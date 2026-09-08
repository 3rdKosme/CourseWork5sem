using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Управление номенклатурой, справочниками и складом (раздел 3.1, 3.5 ТЗ).</summary>
public class AdminCatalogService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit)
{
    // ---------- Товары ----------

    public async Task<PagedResult<AdminProductDto>> SearchAsync(AdminProductQuery query, CancellationToken ct = default)
    {
        var products = db.Products.Include(p => p.Category).Include(p => p.Brand)
            .Include(p => p.Images).Include(p => p.Attributes)
            .AsQueryable();

        if (!query.IncludeInactive) products = products.Where(p => p.IsActive);
        if (query.CategoryId is { } categoryId) products = products.Where(p => p.CategoryId == categoryId);
        if (query.LowStockOnly == true) products = products.Where(p => p.StockQuantity < 5);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") || EF.Functions.Like(p.Sku, $"%{term}%"));
        }

        var page = await products.OrderByDescending(p => p.Id).ToPagedResultAsync(query, ct);
        return page.Map(Map);
    }

    public async Task<AdminProductDto> GetAsync(int id, CancellationToken ct = default) =>
        Map(await LoadProductAsync(id, ct));

    public async Task<AdminProductDto> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        var sku = input.Sku.Trim();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"Товар с артикулом {sku} уже существует");

        await EnsureReferencesAsync(input.CategoryId, input.BrandId, ct);

        var existingSlugs = await db.Products.Select(p => p.Slug).ToListAsync(ct);
        var slug = string.IsNullOrWhiteSpace(input.Slug)
            ? Slug.Unique(input.Name, candidate => existingSlugs.Contains(candidate))
            : Slug.From(input.Slug);

        var product = new Product
        {
            Sku = sku,
            Name = input.Name.Trim(),
            Slug = slug,
            CategoryId = input.CategoryId,
            BrandId = input.BrandId,
            Description = input.Description,
            Price = input.Price,
            OldPrice = input.OldPrice,
            StockQuantity = Math.Max(0, input.StockQuantity),
            IsActive = input.IsActive,
            WarrantyMonths = input.WarrantyMonths,
            WeightGrams = input.WeightGrams
        };

        ApplyImages(product, input.Images);
        ApplyAttributes(product, input.Attributes);

        db.Products.Add(product);

        if (product.StockQuantity > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                Product = product,
                Delta = product.StockQuantity,
                Reason = StockMovementReason.Purchase,
                Comment = "Первичное оприходование при создании товара",
                CreatedByUserId = currentUser.UserId
            });
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ProductCreated", nameof(Product), product.Id, new { product.Sku, product.Name }, ct);

        return Map(await LoadProductAsync(product.Id, ct));
    }

    public async Task<AdminProductDto> UpdateAsync(int id, ProductInput input, CancellationToken ct = default)
    {
        var product = await LoadProductAsync(id, ct);
        var sku = input.Sku.Trim();

        if (sku != product.Sku && await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"Товар с артикулом {sku} уже существует");

        await EnsureReferencesAsync(input.CategoryId, input.BrandId, ct);

        product.Sku = sku;
        product.Name = input.Name.Trim();
        if (!string.IsNullOrWhiteSpace(input.Slug)) product.Slug = Slug.From(input.Slug);
        product.CategoryId = input.CategoryId;
        product.BrandId = input.BrandId;
        product.Description = input.Description;
        product.Price = input.Price;
        product.OldPrice = input.OldPrice;
        product.IsActive = input.IsActive;
        product.WarrantyMonths = input.WarrantyMonths;
        product.WeightGrams = input.WeightGrams;
        product.UpdatedAt = DateTime.UtcNow;

        // Остаток меняется только через движения склада (FR-45), поэтому здесь он не редактируется.
        db.ProductImages.RemoveRange(product.Images);
        product.Images.Clear();
        ApplyImages(product, input.Images);

        db.ProductAttributes.RemoveRange(product.Attributes);
        product.Attributes.Clear();
        ApplyAttributes(product, input.Attributes);

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ProductUpdated", nameof(Product), id, new { product.Sku, product.Name, product.Price }, ct);

        return Map(await LoadProductAsync(id, ct));
    }

    /// <summary>Мягкое удаление: товар исчезает из каталога, но остаётся в истории заказов.</summary>
    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw NotFoundException.For("Товар", id);

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ProductDeactivated", nameof(Product), id, null, ct);
    }

    /// <summary>FR-46, FR-47: движение остатка с обязательным комментарием и запретом ухода в минус.</summary>
    public async Task<AdminProductDto> ApplyStockMovementAsync(int id, StockMovementRequest request, CancellationToken ct = default)
    {
        if (request.Delta == 0)
            throw new BusinessRuleException("Изменение остатка не может быть нулевым");
        if (string.IsNullOrWhiteSpace(request.Comment))
            throw new BusinessRuleException("Укажите комментарий к движению остатка");

        var product = await LoadProductAsync(id, ct);
        var resulting = product.StockQuantity + request.Delta;
        if (resulting < 0)
            throw new ConflictException($"Остаток не может стать отрицательным: текущий {product.StockQuantity}, изменение {request.Delta}");

        product.StockQuantity = resulting;
        product.UpdatedAt = DateTime.UtcNow;

        db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            Delta = request.Delta,
            Reason = request.Reason,
            Comment = request.Comment.Trim(),
            CreatedByUserId = currentUser.UserId
        });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("StockChanged", nameof(Product), id,
            new { request.Delta, Reason = request.Reason.ToString(), request.Comment }, ct);

        return Map(product);
    }

    public async Task<PagedResult<StockMovementDto>> GetStockMovementsAsync(int productId, PageRequest page, CancellationToken ct = default) =>
        await db.StockMovements
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto(m.Id, m.ProductId, m.Product.Name, m.Delta, m.Reason.ToString(),
                m.OrderId, m.Comment, m.CreatedByUser != null ? m.CreatedByUser.FullName : null, m.CreatedAt))
            .ToPagedResultAsync(page, ct);

    // ---------- Категории ----------

    public async Task<List<CategoryDto>> GetCategoriesFlatAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var counts = await db.Products.GroupBy(p => p.CategoryId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return categories
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.Description,
                counts.GetValueOrDefault(c.Id, 0), []))
            .ToList();
    }

    public async Task<CategoryDto> CreateCategoryAsync(CategoryInput input, CancellationToken ct = default)
    {
        var slugs = await db.Categories.Select(c => c.Slug).ToListAsync(ct);
        var category = new Category
        {
            Name = input.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(input.Slug)
                ? Slug.Unique(input.Name, s => slugs.Contains(s))
                : Slug.From(input.Slug),
            ParentId = input.ParentId,
            Description = input.Description,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("CategoryCreated", nameof(Category), category.Id, new { category.Name }, ct);

        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentId, category.Description, 0, []);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, CategoryInput input, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw NotFoundException.For("Категория", id);

        if (input.ParentId == id)
            throw new BusinessRuleException("Категория не может быть родителем самой себе");

        category.Name = input.Name.Trim();
        if (!string.IsNullOrWhiteSpace(input.Slug)) category.Slug = Slug.From(input.Slug);
        category.ParentId = input.ParentId;
        category.Description = input.Description;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("CategoryUpdated", nameof(Category), id, new { category.Name }, ct);

        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentId, category.Description, 0, []);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw NotFoundException.For("Категория", id);

        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException("Нельзя удалить категорию, в которой есть товары");
        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            throw new ConflictException("Нельзя удалить категорию с подкатегориями");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("CategoryDeleted", nameof(Category), id, null, ct);
    }

    // ---------- Бренды ----------

    public async Task<BrandDto> CreateBrandAsync(BrandInput input, CancellationToken ct = default)
    {
        var slugs = await db.Brands.Select(b => b.Slug).ToListAsync(ct);
        var brand = new Brand
        {
            Name = input.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(input.Slug)
                ? Slug.Unique(input.Name, s => slugs.Contains(s))
                : Slug.From(input.Slug),
            Country = input.Country,
            Website = input.Website,
            LogoUrl = input.LogoUrl
        };

        db.Brands.Add(brand);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("BrandCreated", nameof(Brand), brand.Id, new { brand.Name }, ct);

        return new BrandDto(brand.Id, brand.Name, brand.Slug, brand.Country, brand.Website, brand.LogoUrl);
    }

    public async Task<BrandDto> UpdateBrandAsync(int id, BrandInput input, CancellationToken ct = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct)
                    ?? throw NotFoundException.For("Бренд", id);

        brand.Name = input.Name.Trim();
        if (!string.IsNullOrWhiteSpace(input.Slug)) brand.Slug = Slug.From(input.Slug);
        brand.Country = input.Country;
        brand.Website = input.Website;
        brand.LogoUrl = input.LogoUrl;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("BrandUpdated", nameof(Brand), id, new { brand.Name }, ct);

        return new BrandDto(brand.Id, brand.Name, brand.Slug, brand.Country, brand.Website, brand.LogoUrl);
    }

    public async Task DeleteBrandAsync(int id, CancellationToken ct = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct)
                    ?? throw NotFoundException.For("Бренд", id);

        if (await db.Products.AnyAsync(p => p.BrandId == id, ct))
            throw new ConflictException("Нельзя удалить бренд, у которого есть товары");

        db.Brands.Remove(brand);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("BrandDeleted", nameof(Brand), id, null, ct);
    }

    // ---------- Вспомогательное ----------

    private async Task EnsureReferencesAsync(int categoryId, int brandId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
            throw NotFoundException.For("Категория", categoryId);
        if (!await db.Brands.AnyAsync(b => b.Id == brandId, ct))
            throw NotFoundException.For("Бренд", brandId);
    }

    private static void ApplyImages(Product product, List<ProductImageInput>? images)
    {
        if (images is null) return;

        foreach (var image in images)
        {
            product.Images.Add(new ProductImage
            {
                Url = image.Url,
                Alt = image.Alt,
                IsPrimary = image.IsPrimary,
                SortOrder = image.SortOrder
            });
        }

        if (product.Images.Count > 0 && product.Images.All(i => !i.IsPrimary))
            product.Images.First().IsPrimary = true;
    }

    private static void ApplyAttributes(Product product, List<ProductAttributeInput>? attributes)
    {
        if (attributes is null) return;

        foreach (var attribute in attributes)
        {
            product.Attributes.Add(new ProductAttribute
            {
                GroupName = attribute.GroupName,
                Name = attribute.Name,
                Value = attribute.Value,
                Unit = attribute.Unit,
                SortOrder = attribute.SortOrder
            });
        }
    }

    private async Task<Product> LoadProductAsync(int id, CancellationToken ct) =>
        await db.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw NotFoundException.For("Товар", id);

    private static AdminProductDto Map(Product p) => new(
        p.Id, p.Sku, p.Name, p.Slug, p.CategoryId, p.Category?.Name ?? string.Empty,
        p.BrandId, p.Brand?.Name ?? string.Empty, p.Description, p.Price, p.OldPrice,
        p.StockQuantity, p.IsActive, p.WarrantyMonths, p.WeightGrams,
        p.RatingAvg, p.RatingCount, p.SoldCount, p.CreatedAt,
        p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Url, i.Alt, i.IsPrimary, i.SortOrder)).ToList(),
        p.Attributes.OrderBy(a => a.SortOrder)
            .Select(a => new ProductAttributeDto(a.GroupName, a.Name, a.Value, a.Unit, a.SortOrder)).ToList());
}
