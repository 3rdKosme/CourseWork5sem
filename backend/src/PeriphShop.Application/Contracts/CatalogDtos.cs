using PeriphShop.Application.Common;

namespace PeriphShop.Application.Contracts;

/// <summary>Способы сортировки каталога (FR-05).</summary>
public enum ProductSort
{
    New = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    Rating = 3,
    Popular = 4
}

/// <summary>Параметры фильтрации каталога (FR-04).</summary>
public class ProductQuery : PageRequest
{
    public string? Search { get; set; }
    public string? CategorySlug { get; set; }
    public int? CategoryId { get; set; }
    public int[]? BrandIds { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
    public bool? OnlyDiscounted { get; set; }
    public decimal? MinRating { get; set; }
    public ProductSort Sort { get; set; } = ProductSort.New;
}

public record CategoryDto(int Id, string Name, string Slug, int? ParentId, string? Description,
    int ProductCount, List<CategoryDto> Children);

public record BrandDto(int Id, string Name, string Slug, string? Country, string? Website, string? LogoUrl);

public record ProductListItemDto(
    int Id, string Sku, string Name, string Slug, decimal Price, decimal? OldPrice,
    bool InStock, int StockQuantity, decimal RatingAvg, int RatingCount,
    string BrandName, string CategoryName, string? PrimaryImageUrl);

public record ProductImageDto(string Url, string? Alt, bool IsPrimary, int SortOrder);

public record ProductAttributeDto(string? GroupName, string Name, string Value, string? Unit, int SortOrder);

public record ProductDetailsDto(
    int Id, string Sku, string Name, string Slug, string? Description,
    decimal Price, decimal? OldPrice, bool InStock, int StockQuantity,
    decimal RatingAvg, int RatingCount, int WarrantyMonths, int? WeightGrams,
    int CategoryId, string CategoryName, string CategorySlug,
    int BrandId, string BrandName,
    List<ProductImageDto> Images, List<ProductAttributeDto> Attributes);

public record BrandFacetDto(int Id, string Name, int Count);

public record CatalogFacetsDto(List<BrandFacetDto> Brands, decimal MinPrice, decimal MaxPrice);

/// <summary>Страница каталога вместе с фасетами (FR-06).</summary>
public class ProductPageDto : PagedResult<ProductListItemDto>
{
    public CatalogFacetsDto Facets { get; init; } = new([], 0, 0);
}

/// <summary>Подборки для главной страницы.</summary>
public record FeaturedDto(
    List<ProductListItemDto> Bestsellers,
    List<ProductListItemDto> Discounted,
    List<ProductListItemDto> Newest);
