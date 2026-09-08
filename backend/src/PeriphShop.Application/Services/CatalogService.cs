using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Публичный каталог: дерево категорий, поиск с фасетами, карточка товара (FR-01 ... FR-08).</summary>
public class CatalogService(IAppDbContext db)
{
    public async Task<List<CategoryDto>> GetCategoryTreeAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var counts = await db.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        return BuildTree(categories, counts, null);
    }

    public async Task<List<BrandDto>> GetBrandsAsync(CancellationToken ct = default) =>
        await db.Brands
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.Country, b.Website, b.LogoUrl))
            .ToListAsync(ct);

    /// <summary>Поиск товаров с фильтрами, сортировкой, пагинацией и фасетами (FR-03 ... FR-06).</summary>
    public async Task<ProductPageDto> SearchAsync(ProductQuery query, CancellationToken ct = default)
    {
        var filtered = await BuildFilteredQueryAsync(query, ct);

        var facets = await BuildFacetsAsync(filtered, ct);
        var total = await filtered.CountAsync(ct);

        var items = await ApplySort(filtered, query.Sort)
            .Skip(query.Skip).Take(query.PageSize)
            .Select(MapListItem())
            .ToListAsync(ct);

        return new ProductPageDto
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total,
            Facets = facets
        };
    }

    public async Task<ProductDetailsDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, ct)
            ?? throw new NotFoundException($"Товар «{slug}» не найден");

        // FR-08: счётчик просмотров карточки.
        product.ViewCount++;
        await db.SaveChangesAsync(ct);

        return new ProductDetailsDto(
            product.Id, product.Sku, product.Name, product.Slug, product.Description,
            product.Price, product.OldPrice, product.InStock, product.StockQuantity,
            product.RatingAvg, product.RatingCount, product.WarrantyMonths, product.WeightGrams,
            product.CategoryId, product.Category.Name, product.Category.Slug,
            product.BrandId, product.Brand.Name,
            product.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.Url, i.Alt, i.IsPrimary, i.SortOrder)).ToList(),
            product.Attributes.OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
                .Select(a => new ProductAttributeDto(a.GroupName, a.Name, a.Value, a.Unit, a.SortOrder)).ToList());
    }

    /// <summary>FR-07: похожие товары той же категории, ближайшие по цене.</summary>
    public async Task<List<ProductListItemDto>> GetSimilarAsync(int productId, int limit = 8, CancellationToken ct = default)
    {
        var source = await db.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.CategoryId, p.Price })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("Товар", productId);

        return await db.Products
            .Where(p => p.IsActive && p.Id != productId && p.CategoryId == source.CategoryId)
            .OrderBy(p => Math.Abs(p.Price - source.Price))
            .Take(limit)
            .Select(MapListItem())
            .ToListAsync(ct);
    }

    public async Task<FeaturedDto> GetFeaturedAsync(int limit = 8, CancellationToken ct = default)
    {
        var active = db.Products.Where(p => p.IsActive);

        var bestsellers = await active.OrderByDescending(p => p.SoldCount).ThenByDescending(p => p.RatingAvg)
            .Take(limit).Select(MapListItem()).ToListAsync(ct);
        var discounted = await active.Where(p => p.OldPrice != null && p.OldPrice > p.Price)
            .OrderByDescending(p => p.OldPrice!.Value - p.Price)
            .Take(limit).Select(MapListItem()).ToListAsync(ct);
        var newest = await active.OrderByDescending(p => p.CreatedAt)
            .Take(limit).Select(MapListItem()).ToListAsync(ct);

        return new FeaturedDto(bestsellers, discounted, newest);
    }

    /// <summary>Собирает IQueryable по всем фильтрам, кроме сортировки и пагинации.</summary>
    private async Task<IQueryable<Product>> BuildFilteredQueryAsync(ProductQuery query, CancellationToken ct)
    {
        var products = db.Products.Where(p => p.IsActive);

        var categoryId = query.CategoryId;
        if (categoryId is null && !string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            categoryId = await db.Categories
                .Where(c => c.Slug == query.CategorySlug)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
            if (categoryId is null) return products.Where(_ => false);
        }

        if (categoryId is { } id)
        {
            // FR-04: фильтр по категории учитывает всё поддерево.
            var ids = await GetCategoryBranchIdsAsync(id, ct);
            products = products.Where(p => ids.Contains(p.CategoryId));
        }

        if (query.BrandIds is { Length: > 0 })
            products = products.Where(p => query.BrandIds.Contains(p.BrandId));

        if (query.MinPrice is { } min) products = products.Where(p => p.Price >= min);
        if (query.MaxPrice is { } max) products = products.Where(p => p.Price <= max);
        if (query.InStock == true) products = products.Where(p => p.StockQuantity > 0);
        if (query.OnlyDiscounted == true) products = products.Where(p => p.OldPrice != null && p.OldPrice > p.Price);
        if (query.MinRating is { } rating) products = products.Where(p => p.RatingAvg >= rating);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Sku, $"%{term}%") ||
                (p.Description != null && EF.Functions.Like(p.Description, $"%{term}%")));
        }

        return products;
    }

    private static async Task<CatalogFacetsDto> BuildFacetsAsync(IQueryable<Product> filtered, CancellationToken ct)
    {
        var brands = await filtered
            .GroupBy(p => new { p.BrandId, p.Brand.Name })
            .Select(g => new BrandFacetDto(g.Key.BrandId, g.Key.Name, g.Count()))
            .OrderByDescending(b => b.Count).ThenBy(b => b.Name)
            .ToListAsync(ct);

        var bounds = await filtered
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(p => p.Price), Max = g.Max(p => p.Price) })
            .FirstOrDefaultAsync(ct);

        return new CatalogFacetsDto(brands, bounds?.Min ?? 0, bounds?.Max ?? 0);
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> products, ProductSort sort) => sort switch
    {
        ProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
        ProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
        ProductSort.Rating => products.OrderByDescending(p => p.RatingAvg).ThenByDescending(p => p.RatingCount),
        ProductSort.Popular => products.OrderByDescending(p => p.SoldCount).ThenByDescending(p => p.ViewCount),
        _ => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
    };

    /// <summary>Идентификаторы категории и всех её потомков (глубина каталога ограничена тремя уровнями).</summary>
    private async Task<List<int>> GetCategoryBranchIdsAsync(int rootId, CancellationToken ct)
    {
        var all = await db.Categories.Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
        var result = new List<int> { rootId };
        var frontier = new List<int> { rootId };

        while (frontier.Count > 0)
        {
            var children = all.Where(c => c.ParentId != null && frontier.Contains(c.ParentId.Value))
                .Select(c => c.Id).ToList();
            if (children.Count == 0) break;

            result.AddRange(children);
            frontier = children;
        }

        return result;
    }

    private static List<CategoryDto> BuildTree(List<Category> all, Dictionary<int, int> counts, int? parentId) =>
        all.Where(c => c.ParentId == parentId)
            .Select(c =>
            {
                var children = BuildTree(all, counts, c.Id);
                var own = counts.GetValueOrDefault(c.Id, 0);
                return new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.Description,
                    own + children.Sum(x => x.ProductCount), children);
            })
            .ToList();

    internal static System.Linq.Expressions.Expression<Func<Product, ProductListItemDto>> MapListItem() =>
        p => new ProductListItemDto(
            p.Id, p.Sku, p.Name, p.Slug, p.Price, p.OldPrice,
            p.StockQuantity > 0, p.StockQuantity, p.RatingAvg, p.RatingCount,
            p.Brand.Name, p.Category.Name,
            p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                .Select(i => i.Url).FirstOrDefault());
}
