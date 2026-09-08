using PeriphShop.Domain.Enums;

namespace PeriphShop.Domain.Entities;

/// <summary>Корзина: либо пользователя, либо анонимная по GUID (FR-20).</summary>
public class Cart
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? AnonymousId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Заказ (FR-30 ... FR-41).</summary>
public class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.New;

    public decimal ItemsTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal Total { get; set; }

    public int? PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; }
    public string? DeliveryAddress { get; set; }
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public string? Comment { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
}

/// <summary>Позиция заказа со снимком наименования, артикула и цены (FR-34).</summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>Запись истории смены статуса заказа (FR-39).</summary>
public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Промокод (FR-55 ... FR-57).</summary>
public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinOrderTotal { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; set; } = [];

    /// <summary>Расчёт скидки для суммы позиций; скидка не превышает саму сумму (FR-57).</summary>
    public decimal CalculateDiscount(decimal itemsTotal)
    {
        var raw = DiscountType == DiscountType.Percent
            ? itemsTotal * DiscountValue / 100m
            : DiscountValue;
        return Math.Round(Math.Min(raw, itemsTotal), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Проверка применимости кода к сумме позиций; сообщение — причина отказа или описание скидки.</summary>
    public bool IsApplicable(decimal itemsTotal, DateTime now, out string message)
    {
        if (!IsActive) { message = "Промокод отключён"; return false; }
        if (ValidFrom.HasValue && now < ValidFrom.Value) { message = "Промокод ещё не действует"; return false; }
        if (ValidTo.HasValue && now > ValidTo.Value) { message = "Срок действия промокода истёк"; return false; }
        if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value) { message = "Промокод исчерпан"; return false; }
        if (itemsTotal < MinOrderTotal)
        {
            message = $"Минимальная сумма заказа для промокода — {MinOrderTotal:0.##} руб.";
            return false;
        }

        message = DiscountType == DiscountType.Percent
            ? $"Скидка {DiscountValue:0.##}%"
            : $"Скидка {DiscountValue:0.##} руб.";
        return true;
    }
}

/// <summary>Движение складского остатка (FR-45).</summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Delta { get; set; }
    public StockMovementReason Reason { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string? Comment { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Запись журнала аудита (FR-70).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Action { get; set; } = null!;
    public string Entity { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? Payload { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
