namespace PeriphShop.Domain.Entities;

/// <summary>Категория каталога, иерархия до трёх уровней (FR-01).</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>Производитель товара.</summary>
public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Country { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}

/// <summary>Товар (FR-02).</summary>
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public int SoldCount { get; set; }
    public int ViewCount { get; set; }
    public int WarrantyMonths { get; set; } = 12;
    public int? WeightGrams { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductAttribute> Attributes { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];

    public bool InStock => StockQuantity > 0;
    public bool HasDiscount => OldPrice.HasValue && OldPrice.Value > Price;
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string? Alt { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>Характеристика товара: группа, имя, значение, единица измерения.</summary>
public class ProductAttribute
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string? GroupName { get; set; }
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Отзыв покупателя, публикуется после модерации (FR-50, FR-51).</summary>
public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public byte Rating { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = null!;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Избранный товар пользователя.</summary>
public class Favorite
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
