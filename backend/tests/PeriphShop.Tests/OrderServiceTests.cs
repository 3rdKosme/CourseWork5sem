using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Tests;

/// <summary>Проверки жизненного цикла заказа (FR-30 ... FR-38).</summary>
public class OrderServiceTests
{
    private static CreateOrderRequest Pickup(string? promo = null) =>
        new(DeliveryMethod.Pickup, null, "Иванов Иван", "+79000000000", PaymentMethod.CashOnDelivery, null, promo);

    [Fact]
    public async Task Создание_заказа_списывает_остаток_и_очищает_корзину()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Keyboard, 2);

        var order = await harness.CreateOrderService().CreateAsync(Pickup());

        order.Status.Should().Be(nameof(OrderStatus.New));
        order.ItemsTotal.Should().Be(20000m);
        order.Total.Should().Be(20000m);
        order.Number.Should().MatchRegex(@"^ORD-\d{8}-\d{4}$");

        var product = await harness.Db.Products.FirstAsync(p => p.Id == harness.Keyboard.Id);
        product.StockQuantity.Should().Be(3);
        product.SoldCount.Should().Be(2);

        (await harness.Db.CartItems.CountAsync()).Should().Be(0);
        (await harness.Db.StockMovements.CountAsync(m => m.Reason == StockMovementReason.Sale)).Should().Be(1);
        harness.Metrics.OrdersCreated.Should().Be(1);
    }

    [Fact]
    public async Task Заказ_при_нехватке_остатка_отклоняется_и_не_меняет_склад()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Mouse, 2);

        // Кто-то успел купить последнюю мышь раньше.
        harness.Mouse.StockQuantity = 1;
        await harness.Db.SaveChangesAsync();

        var act = () => harness.CreateOrderService().CreateAsync(Pickup());

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*доступно 1*");
        (await harness.Db.Orders.CountAsync()).Should().Be(0);
        (await harness.Db.Products.FirstAsync(p => p.Id == harness.Mouse.Id)).StockQuantity.Should().Be(1);
        harness.Metrics.Failures.Should().Contain("out_of_stock");
    }

    [Fact]
    public async Task Пустая_корзина_не_позволяет_оформить_заказ()
    {
        using var harness = new TestHarness();

        var act = () => harness.CreateOrderService().CreateAsync(Pickup());

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("Корзина пуста");
    }

    [Fact]
    public async Task Отмена_заказа_возвращает_остаток_на_склад()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Keyboard, 2);

        var service = harness.CreateOrderService();
        var order = await service.CreateAsync(Pickup());

        var cancelled = await service.CancelAsync(order.Id, new CancelOrderRequest("передумал"));

        cancelled.Status.Should().Be(nameof(OrderStatus.Cancelled));
        (await harness.Db.Products.FirstAsync(p => p.Id == harness.Keyboard.Id)).StockQuantity.Should().Be(5);
        (await harness.Db.StockMovements.CountAsync(m => m.Reason == StockMovementReason.Cancellation)).Should().Be(1);
    }

    [Fact]
    public async Task Недопустимый_переход_статуса_отклоняется()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Keyboard, 1);

        var service = harness.CreateOrderService();
        var order = await service.CreateAsync(Pickup());

        harness.CurrentUser.Role = UserRole.Manager;
        var act = () => service.ChangeStatusAsync(order.Id, new ChangeOrderStatusRequest(OrderStatus.Delivered, null));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Недопустимый переход*");
    }

    [Fact]
    public async Task Промокод_уменьшает_сумму_и_увеличивает_счётчик_использований()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Keyboard, 1);

        harness.Db.PromoCodes.Add(new Domain.Entities.PromoCode
        {
            Id = 1,
            Code = "WELCOME10",
            DiscountType = DiscountType.Percent,
            DiscountValue = 10m,
            MinOrderTotal = 5000m
        });
        await harness.Db.SaveChangesAsync();

        var order = await harness.CreateOrderService().CreateAsync(Pickup("welcome10"));

        order.DiscountTotal.Should().Be(1000m);
        order.Total.Should().Be(9000m);
        (await harness.Db.PromoCodes.FirstAsync()).UsedCount.Should().Be(1);
    }

    [Fact]
    public async Task Промокод_ниже_минимальной_суммы_не_применяется()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Mouse, 1); // 5000 руб.

        harness.Db.PromoCodes.Add(new Domain.Entities.PromoCode
        {
            Id = 1, Code = "BIG", DiscountType = DiscountType.Amount, DiscountValue = 1000m, MinOrderTotal = 10000m
        });
        await harness.Db.SaveChangesAsync();

        var act = () => harness.CreateOrderService().CreateAsync(Pickup("BIG"));

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Минимальная сумма*");
        (await harness.Db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Чужой_заказ_недоступен_покупателю()
    {
        using var harness = new TestHarness();
        await harness.AddToCartAsync(harness.Keyboard, 1);

        var order = await harness.CreateOrderService().CreateAsync(Pickup());

        harness.CurrentUser.UserId = 999;
        var act = () => harness.CreateOrderService().GetByIdAsync(order.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData(DeliveryMethod.Pickup, 1000, 0)]
    [InlineData(DeliveryMethod.Courier, 1000, 350)]
    [InlineData(DeliveryMethod.Courier, 5000, 0)]
    [InlineData(DeliveryMethod.PostMachine, 9000, 200)]
    public void Стоимость_доставки_соответствует_тарифам(DeliveryMethod method, decimal itemsTotal, decimal expected) =>
        OrderService.CalculateDeliveryCost(method, itemsTotal).Should().Be(expected);
}
