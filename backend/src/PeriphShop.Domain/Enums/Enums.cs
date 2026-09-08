namespace PeriphShop.Domain.Enums;

/// <summary>Роль пользователя в системе (RBAC, раздел 2 ТЗ).</summary>
public enum UserRole
{
    Customer = 0,
    Manager = 1,
    Admin = 2
}

/// <summary>Статус заказа. Переходы описаны в <see cref="Rules.OrderStatusRules"/>.</summary>
public enum OrderStatus
{
    New = 0,
    Paid = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Completed = 5,
    Cancelled = 6,
    Refunded = 7
}

public enum DeliveryMethod
{
    Pickup = 0,
    Courier = 1,
    PostMachine = 2
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    CardOnline = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Refunded = 2
}

/// <summary>Причина движения складского остатка (FR-45).</summary>
public enum StockMovementReason
{
    Purchase = 0,
    Sale = 1,
    Cancellation = 2,
    Correction = 3,
    WriteOff = 4
}

public enum DiscountType
{
    Percent = 0,
    Amount = 1
}
