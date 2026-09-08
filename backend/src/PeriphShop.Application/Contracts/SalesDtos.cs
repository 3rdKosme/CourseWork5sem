using PeriphShop.Domain.Enums;

namespace PeriphShop.Application.Contracts;

// ---------- Корзина ----------

public record AddCartItemRequest(int ProductId, int Quantity);

public record UpdateCartItemRequest(int Quantity);

public record CartItemDto(
    int ProductId, string Name, string Slug, decimal UnitPrice, int Quantity,
    decimal LineTotal, int StockQuantity, string? ImageUrl, bool HasStockIssue);

public record CartDto(List<CartItemDto> Items, decimal ItemsTotal, int ItemsCount, bool HasIssues);

// ---------- Промокоды ----------

public record ValidatePromoRequest(string Code, decimal ItemsTotal);

public record PromoValidationDto(bool Valid, decimal Discount, string Message);

// ---------- Заказы ----------

public record CreateOrderRequest(
    DeliveryMethod DeliveryMethod,
    string? DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    PaymentMethod PaymentMethod,
    string? Comment,
    string? PromoCode);

public record OrderItemDto(int? ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderStatusHistoryDto(string? FromStatus, string ToStatus, string? ChangedBy, string? Comment, DateTime CreatedAt);

public record OrderListItemDto(
    int Id, string Number, string Status, decimal Total, int ItemsCount,
    string DeliveryMethod, string PaymentStatus, DateTime CreatedAt);

public record OrderDetailsDto(
    int Id, string Number, string Status, decimal ItemsTotal, decimal DiscountTotal,
    decimal DeliveryCost, decimal Total, string DeliveryMethod, string? DeliveryAddress,
    string RecipientName, string RecipientPhone, string? Comment,
    string PaymentMethod, string PaymentStatus, string? PromoCode,
    DateTime CreatedAt, DateTime? PaidAt, DateTime? CompletedAt, DateTime? CancelledAt, string? CancelReason,
    string? CustomerEmail, string? CustomerName,
    List<OrderItemDto> Items, List<OrderStatusHistoryDto> StatusHistory,
    List<string> AllowedNextStatuses);

public record CancelOrderRequest(string? Reason);

// ---------- Отзывы ----------

public record CreateReviewRequest(int ProductId, byte Rating, string? Title, string Body);

public record ReviewDto(int Id, int ProductId, string? ProductName, string AuthorName, byte Rating,
    string? Title, string Body, bool IsApproved, DateTime CreatedAt);

// ---------- Избранное ----------

public record FavoriteDto(int ProductId, string Name, string Slug, decimal Price, string? ImageUrl, bool InStock);
