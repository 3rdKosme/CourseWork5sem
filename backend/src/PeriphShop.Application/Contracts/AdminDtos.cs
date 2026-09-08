using PeriphShop.Application.Common;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Application.Contracts;

// ---------- Товары ----------

public class AdminProductQuery : PageRequest
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool IncludeInactive { get; set; } = true;
    public bool? LowStockOnly { get; set; }
}

public record ProductImageInput(string Url, string? Alt, bool IsPrimary, int SortOrder);

public record ProductAttributeInput(string? GroupName, string Name, string Value, string? Unit, int SortOrder);

public record ProductInput(
    string Sku, string Name, string? Slug, int CategoryId, int BrandId, string? Description,
    decimal Price, decimal? OldPrice, int StockQuantity, bool IsActive,
    int WarrantyMonths, int? WeightGrams,
    List<ProductImageInput>? Images, List<ProductAttributeInput>? Attributes);

public record AdminProductDto(
    int Id, string Sku, string Name, string Slug, int CategoryId, string CategoryName,
    int BrandId, string BrandName, string? Description, decimal Price, decimal? OldPrice,
    int StockQuantity, bool IsActive, int WarrantyMonths, int? WeightGrams,
    decimal RatingAvg, int RatingCount, int SoldCount, DateTime CreatedAt,
    List<ProductImageDto> Images, List<ProductAttributeDto> Attributes);

public record StockMovementRequest(int Delta, StockMovementReason Reason, string? Comment);

public record StockMovementDto(int Id, int ProductId, string ProductName, int Delta, string Reason,
    int? OrderId, string? Comment, string? CreatedBy, DateTime CreatedAt);

// ---------- Справочники ----------

public record CategoryInput(string Name, string? Slug, int? ParentId, string? Description, int SortOrder, bool IsActive);

public record BrandInput(string Name, string? Slug, string? Country, string? Website, string? LogoUrl);

// ---------- Заказы ----------

public class AdminOrderQuery : PageRequest
{
    public OrderStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record ChangeOrderStatusRequest(OrderStatus Status, string? Comment);

// ---------- Отзывы ----------

public class AdminReviewQuery : PageRequest
{
    public bool? Approved { get; set; }
}

// ---------- Промокоды ----------

public record PromoCodeInput(
    string Code, DiscountType DiscountType, decimal DiscountValue, decimal MinOrderTotal,
    DateTime? ValidFrom, DateTime? ValidTo, int? UsageLimit, bool IsActive);

public record PromoCodeDto(
    int Id, string Code, string DiscountType, decimal DiscountValue, decimal MinOrderTotal,
    DateTime? ValidFrom, DateTime? ValidTo, int? UsageLimit, int UsedCount, bool IsActive);

// ---------- Пользователи ----------

public class AdminUserQuery : PageRequest
{
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
}

public record AdminUserDto(int Id, string Email, string FullName, string? Phone, string Role,
    bool IsActive, int OrdersCount, DateTime CreatedAt);

public record ChangeRoleRequest(UserRole Role);

// ---------- Отчёты и аудит ----------

public record SalesSummaryDto(
    DateTime From, DateTime To, int OrdersCount, decimal Revenue, decimal AverageCheck,
    int NewCustomers, int ItemsSold, Dictionary<string, int> OrdersByStatus);

public record TopProductDto(int? ProductId, string ProductName, string Sku, int Quantity, decimal Revenue);

public record LowStockDto(int Id, string Sku, string Name, int StockQuantity, bool IsActive);

public record SalesByDayDto(DateOnly Date, int OrdersCount, decimal Revenue);

public class AuditQuery : PageRequest
{
    public string? Entity { get; set; }
    public int? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record AuditLogDto(long Id, int? UserId, string? UserEmail, string Action, string Entity,
    string? EntityId, string? Payload, string? Ip, DateTime CreatedAt);
