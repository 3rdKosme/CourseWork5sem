using PeriphShop.Domain.Enums;

namespace PeriphShop.Domain.Rules;

/// <summary>
/// Граф допустимых переходов статусов заказа (FR-36).
/// Единственный источник истины: используется и API менеджера, и отменой заказа покупателем.
/// </summary>
public static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.New] = [OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [OrderStatus.Completed, OrderStatus.Refunded],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Refunded] = []
    };

    /// <summary>Статусы, из которых остаток уже списан и должен быть возвращён при отмене (FR-38).</summary>
    public static readonly OrderStatus[] StockReleasingStatuses = [OrderStatus.Cancelled, OrderStatus.Refunded];

    /// <summary>Статусы, в которых покупатель вправе отменить заказ самостоятельно (FR-37).</summary>
    public static readonly OrderStatus[] CustomerCancellableStatuses = [OrderStatus.New, OrderStatus.Paid];

    public static IReadOnlyList<OrderStatus> AllowedNext(OrderStatus current) =>
        Transitions.TryGetValue(current, out var next) ? next : [];

    public static bool CanTransition(OrderStatus from, OrderStatus to) => AllowedNext(from).Contains(to);
}
