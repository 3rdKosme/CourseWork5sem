using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;
using PeriphShop.Domain.Rules;

namespace PeriphShop.Application.Services;

/// <summary>
/// Оформление и жизненный цикл заказа (FR-30 ... FR-41).
/// Создание заказа и возврат остатков выполняются в транзакции.
/// </summary>
public class OrderService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ICartService carts,
    IBusinessMetrics metrics,
    IEmailSender email,
    IAuditService audit,
    ILogger<OrderService> logger)
{
    private const decimal CourierCost = 350m;
    private const decimal PostMachineCost = 200m;
    private const decimal FreeCourierThreshold = 5000m;

    /// <summary>FR-32: тариф доставки зависит от способа и суммы позиций.</summary>
    public static decimal CalculateDeliveryCost(DeliveryMethod method, decimal itemsTotal) => method switch
    {
        DeliveryMethod.Pickup => 0m,
        DeliveryMethod.Courier => itemsTotal >= FreeCourierThreshold ? 0m : CourierCost,
        DeliveryMethod.PostMachine => PostMachineCost,
        _ => 0m
    };

    public async Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Требуется аутентификация");

        if (request.DeliveryMethod != DeliveryMethod.Pickup && string.IsNullOrWhiteSpace(request.DeliveryAddress))
        {
            metrics.CheckoutFailed("address_required");
            throw new ValidationAppException("Для выбранного способа доставки требуется адрес",
                new Dictionary<string, string[]> { ["deliveryAddress"] = ["Укажите адрес доставки"] });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        var order = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var cart = await carts.ResolveCartAsync(createIfMissing: false, ct);
            if (cart.Items.Count == 0)
            {
                metrics.CheckoutFailed("empty_cart");
                throw new BusinessRuleException("Корзина пуста");
            }

            var productIds = cart.Items.Select(i => i.ProductId).ToList();
            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            // FR-34: проверка остатков до любых изменений.
            var shortages = new List<string>();
            foreach (var item in cart.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                {
                    shortages.Add($"Товар #{item.ProductId} больше не продаётся");
                    continue;
                }

                if (product.StockQuantity < item.Quantity)
                    shortages.Add($"{product.Name}: доступно {product.StockQuantity}, запрошено {item.Quantity}");
            }

            if (shortages.Count > 0)
            {
                metrics.CheckoutFailed("out_of_stock");
                throw new ConflictException("Недостаточно товара на складе. " + string.Join("; ", shortages));
            }

            var itemsTotal = Math.Round(
                cart.Items.Sum(i => products[i.ProductId].Price * i.Quantity), 2, MidpointRounding.AwayFromZero);

            var (promo, discount) = await ResolvePromoAsync(request.PromoCode, itemsTotal, ct);
            var deliveryCost = CalculateDeliveryCost(request.DeliveryMethod, itemsTotal);

            var newOrder = new Order
            {
                Number = await NextOrderNumberAsync(ct),
                UserId = userId,
                Status = OrderStatus.New,
                ItemsTotal = itemsTotal,
                DiscountTotal = discount,
                DeliveryCost = deliveryCost,
                Total = Math.Round(itemsTotal - discount + deliveryCost, 2, MidpointRounding.AwayFromZero),
                PromoCodeId = promo?.Id,
                DeliveryMethod = request.DeliveryMethod,
                DeliveryAddress = request.DeliveryAddress?.Trim(),
                RecipientName = request.RecipientName.Trim(),
                RecipientPhone = request.RecipientPhone.Trim(),
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = PaymentStatus.Pending
            };

            foreach (var item in cart.Items)
            {
                var product = products[item.ProductId];

                newOrder.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity,
                    LineTotal = Math.Round(product.Price * item.Quantity, 2, MidpointRounding.AwayFromZero)
                });

                product.StockQuantity -= item.Quantity;
                product.SoldCount += item.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    Delta = -item.Quantity,
                    Reason = StockMovementReason.Sale,
                    Order = newOrder,
                    CreatedByUserId = userId,
                    Comment = "Списание при оформлении заказа"
                });
            }

            newOrder.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = null,
                ToStatus = OrderStatus.New,
                ChangedByUserId = userId,
                Comment = "Заказ создан"
            });

            if (promo is not null) promo.UsedCount++;

            db.Orders.Add(newOrder);
            db.CartItems.RemoveRange(cart.Items);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return newOrder;
        });

        metrics.OrderCreated(order.Total);
        logger.LogInformation("Создан заказ {Number} пользователя {UserId} на сумму {Total}",
            order.Number, userId, order.Total);
        await audit.WriteAsync("OrderCreated", nameof(Order), order.Id,
            new { order.Number, order.Total, ItemsCount = order.Items.Count }, ct);
        await NotifyAsync(order, $"Заказ {order.Number} принят",
            $"Мы приняли заказ {order.Number} на сумму {order.Total:0.00} руб. Статус можно отслеживать в личном кабинете.", ct);

        return await GetByIdAsync(order.Id, ct);
    }

    public async Task<PagedResult<OrderListItemDto>> GetMyOrdersAsync(PageRequest page, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Требуется аутентификация");

        return await db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListItemDto(
                o.Id, o.Number, o.Status.ToString(), o.Total, o.Items.Sum(i => i.Quantity),
                o.DeliveryMethod.ToString(), o.PaymentStatus.ToString(), o.CreatedAt))
            .ToPagedResultAsync(page, ct);
    }

    /// <summary>Детали заказа. Покупатель видит только свой заказ (проверка владения ресурсом).</summary>
    public async Task<OrderDetailsDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        EnsureCanView(order);
        return Map(order);
    }

    public async Task<OrderDetailsDto> CancelAsync(int id, CancelOrderRequest request, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        EnsureCanView(order);

        var isStaff = currentUser.Role is UserRole.Manager or UserRole.Admin;
        if (!isStaff && !OrderStatusRules.CustomerCancellableStatuses.Contains(order.Status))
            throw new ConflictException($"Заказ в статусе «{order.Status}» может отменить только менеджер");

        await ChangeStatusInternalAsync(order, OrderStatus.Cancelled, request.Reason ?? "Отменён покупателем", ct);
        return Map(order);
    }

    /// <summary>FR-40: имитация онлайн-оплаты картой.</summary>
    public async Task<OrderDetailsDto> PayAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        EnsureCanView(order);

        if (order.PaymentMethod != PaymentMethod.CardOnline)
            throw new BusinessRuleException("Заказ оформлен с оплатой при получении");
        if (order.Status != OrderStatus.New)
            throw new ConflictException($"Оплата невозможна в статусе «{order.Status}»");

        await ChangeStatusInternalAsync(order, OrderStatus.Paid, "Оплата картой (имитация шлюза)", ct);
        return Map(order);
    }

    /// <summary>Смена статуса менеджером (FR-36, FR-39). Возврат остатков — при отмене и возврате (FR-38).</summary>
    public async Task<OrderDetailsDto> ChangeStatusAsync(int id, ChangeOrderStatusRequest request, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        await ChangeStatusInternalAsync(order, request.Status, request.Comment, ct);
        return Map(order);
    }

    private async Task ChangeStatusInternalAsync(Order order, OrderStatus target, string? comment, CancellationToken ct)
    {
        if (!OrderStatusRules.CanTransition(order.Status, target))
        {
            var allowed = OrderStatusRules.AllowedNext(order.Status);
            throw new ConflictException(
                $"Недопустимый переход «{order.Status}» → «{target}». Разрешено: " +
                (allowed.Count == 0 ? "нет переходов" : string.Join(", ", allowed)));
        }

        var from = order.Status;
        var now = DateTime.UtcNow;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            order.Status = target;

            switch (target)
            {
                case OrderStatus.Paid:
                    order.PaidAt = now;
                    order.PaymentStatus = PaymentStatus.Paid;
                    break;
                case OrderStatus.Completed:
                    order.CompletedAt = now;
                    if (order.PaymentStatus == PaymentStatus.Pending) order.PaymentStatus = PaymentStatus.Paid;
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledAt = now;
                    order.CancelReason = comment;
                    await ReturnStockAsync(order, StockMovementReason.Cancellation, ct);
                    break;
                case OrderStatus.Refunded:
                    order.PaymentStatus = PaymentStatus.Refunded;
                    await ReturnStockAsync(order, StockMovementReason.Cancellation, ct);
                    break;
            }

            db.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = from,
                ToStatus = target,
                ChangedByUserId = currentUser.UserId,
                Comment = comment
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        });

        metrics.OrderStatusChanged(from, target);
        logger.LogInformation("Заказ {Number}: статус {From} → {To}", order.Number, from, target);
        await audit.WriteAsync("OrderStatusChanged", nameof(Order), order.Id, new { From = from.ToString(), To = target.ToString(), comment }, ct);
        await NotifyAsync(order, $"Заказ {order.Number}: статус изменён",
            $"Новый статус заказа {order.Number}: {target}." + (comment is null ? "" : $" Комментарий: {comment}"), ct);
    }

    /// <summary>FR-38: возврат остатков на склад с записью движения.</summary>
    private async Task ReturnStockAsync(Order order, StockMovementReason reason, CancellationToken ct)
    {
        var productIds = order.Items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in order.Items)
        {
            if (item.ProductId is null || !products.TryGetValue(item.ProductId.Value, out var product)) continue;

            product.StockQuantity += item.Quantity;
            product.SoldCount = Math.Max(0, product.SoldCount - item.Quantity);

            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                Delta = item.Quantity,
                Reason = reason,
                OrderId = order.Id,
                CreatedByUserId = currentUser.UserId,
                Comment = $"Возврат остатка по заказу {order.Number}"
            });
        }
    }

    private async Task<(PromoCode? promo, decimal discount)> ResolvePromoAsync(string? code, decimal itemsTotal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return (null, 0m);

        var normalized = code.Trim().ToUpperInvariant();
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Code == normalized, ct);
        if (promo is null)
        {
            metrics.CheckoutFailed("promo_not_found");
            throw new BusinessRuleException("Промокод не найден");
        }

        if (!promo.IsApplicable(itemsTotal, DateTime.UtcNow, out var message))
        {
            metrics.CheckoutFailed("promo_not_applicable");
            throw new BusinessRuleException(message);
        }

        return (promo, promo.CalculateDiscount(itemsTotal));
    }

    /// <summary>FR-35: номер вида ORD-ГГГГММДД-NNNN с суточным счётчиком.</summary>
    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"ORD-{today:yyyyMMdd}-";
        var todayCount = await db.Orders.CountAsync(o => o.CreatedAt >= today && o.CreatedAt < today.AddDays(1), ct);
        return prefix + (todayCount + 1).ToString("D4");
    }

    private async Task<Order> LoadOrderAsync(int id, CancellationToken ct) =>
        await db.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .Include(o => o.PromoCode)
            .Include(o => o.StatusHistory).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw NotFoundException.For("Заказ", id);

    private void EnsureCanView(Order order)
    {
        var isStaff = currentUser.Role is UserRole.Manager or UserRole.Admin;
        if (!isStaff && order.UserId != currentUser.UserId)
            throw new ForbiddenException("Заказ принадлежит другому пользователю");
    }

    private async Task NotifyAsync(Order order, string subject, string body, CancellationToken ct)
    {
        var to = order.User?.Email ?? await db.Users.Where(u => u.Id == order.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(to)) return;

        try
        {
            await email.SendAsync(to, subject, body, ct);
        }
        catch (Exception ex)
        {
            // Сбой уведомления не должен отменять уже выполненную бизнес-операцию.
            logger.LogWarning(ex, "Не удалось отправить уведомление по заказу {Number}", order.Number);
        }
    }

    internal static OrderDetailsDto Map(Order o) => new(
        o.Id, o.Number, o.Status.ToString(), o.ItemsTotal, o.DiscountTotal, o.DeliveryCost, o.Total,
        o.DeliveryMethod.ToString(), o.DeliveryAddress, o.RecipientName, o.RecipientPhone, o.Comment,
        o.PaymentMethod.ToString(), o.PaymentStatus.ToString(), o.PromoCode?.Code,
        o.CreatedAt, o.PaidAt, o.CompletedAt, o.CancelledAt, o.CancelReason,
        o.User?.Email, o.User?.FullName,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
        o.StatusHistory.OrderBy(h => h.CreatedAt)
            .Select(h => new OrderStatusHistoryDto(h.FromStatus?.ToString(), h.ToStatus.ToString(),
                h.ChangedByUser?.FullName, h.Comment, h.CreatedAt)).ToList(),
        OrderStatusRules.AllowedNext(o.Status).Select(s => s.ToString()).ToList());
}
