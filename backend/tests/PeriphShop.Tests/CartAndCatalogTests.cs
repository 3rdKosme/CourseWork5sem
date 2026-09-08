using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;
using PeriphShop.Domain.Rules;

namespace PeriphShop.Tests;

/// <summary>Проверки корзины (FR-20 ... FR-24).</summary>
public class CartServiceTests
{
    [Fact]
    public async Task Добавление_товара_ограничено_остатком()
    {
        using var harness = new TestHarness();
        var cart = harness.CreateCartService();

        var result = await cart.AddItemAsync(new AddCartItemRequest(harness.Mouse.Id, 10));

        result.Items.Should().ContainSingle();
        result.Items[0].Quantity.Should().Be(2); // остаток мыши = 2
        result.ItemsTotal.Should().Be(10000m);
    }

    [Fact]
    public async Task Повторное_добавление_суммирует_количество()
    {
        using var harness = new TestHarness();
        var cart = harness.CreateCartService();

        await cart.AddItemAsync(new AddCartItemRequest(harness.Keyboard.Id, 1));
        var result = await cart.AddItemAsync(new AddCartItemRequest(harness.Keyboard.Id, 2));

        result.Items.Should().ContainSingle();
        result.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Товар_без_остатка_в_корзину_не_добавляется()
    {
        using var harness = new TestHarness();
        harness.Mouse.StockQuantity = 0;
        await harness.Db.SaveChangesAsync();

        var act = () => harness.CreateCartService().AddItemAsync(new AddCartItemRequest(harness.Mouse.Id, 1));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Позиция_с_нехваткой_остатка_помечается_флагом()
    {
        using var harness = new TestHarness();
        var cart = harness.CreateCartService();
        await cart.AddItemAsync(new AddCartItemRequest(harness.Keyboard.Id, 4));

        harness.Keyboard.StockQuantity = 1;
        await harness.Db.SaveChangesAsync();

        var result = await cart.GetAsync();

        result.HasIssues.Should().BeTrue();
        result.Items[0].HasStockIssue.Should().BeTrue();
    }

    [Fact]
    public async Task Анонимная_корзина_сливается_с_корзиной_пользователя()
    {
        using var harness = new TestHarness();

        // Гость складывает товары в анонимную корзину.
        harness.CurrentUser.UserId = null;
        harness.CurrentUser.AnonymousCartId = "guest-cart-1";
        var guestCart = harness.CreateCartService();
        await guestCart.AddItemAsync(new AddCartItemRequest(harness.Keyboard.Id, 2));
        await guestCart.AddItemAsync(new AddCartItemRequest(harness.Mouse.Id, 1));

        // Тот же посетитель входит в систему, где уже есть одна клавиатура.
        harness.CurrentUser.UserId = harness.Customer.Id;
        var userCart = harness.CreateCartService();
        await userCart.AddItemAsync(new AddCartItemRequest(harness.Keyboard.Id, 1));

        await userCart.MergeAnonymousCartAsync(harness.Customer.Id, "guest-cart-1");
        var result = await userCart.GetAsync();

        result.Items.Should().HaveCount(2);
        result.Items.Single(i => i.ProductId == harness.Keyboard.Id).Quantity.Should().Be(3);
        (await harness.Db.Carts.CountAsync(c => c.AnonymousId == "guest-cart-1")).Should().Be(0);
    }
}

/// <summary>Проверки правил домена и вспомогательных функций.</summary>
public class DomainRulesTests
{
    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.New, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.New, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled, false)]
    public void Переходы_статусов_соответствуют_графу(OrderStatus from, OrderStatus to, bool allowed) =>
        OrderStatusRules.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void Скидка_в_процентах_не_превышает_сумму_позиций()
    {
        var promo = new PromoCode { DiscountType = DiscountType.Percent, DiscountValue = 150m };

        promo.CalculateDiscount(1000m).Should().Be(1000m);
    }

    [Fact]
    public void Истёкший_промокод_не_применяется()
    {
        var promo = new PromoCode
        {
            DiscountType = DiscountType.Amount,
            DiscountValue = 500m,
            ValidTo = DateTime.UtcNow.AddDays(-1)
        };

        promo.IsApplicable(10000m, DateTime.UtcNow, out var message).Should().BeFalse();
        message.Should().Contain("истёк");
    }

    [Fact]
    public void Исчерпанный_промокод_не_применяется()
    {
        var promo = new PromoCode
        {
            DiscountType = DiscountType.Percent, DiscountValue = 10m, UsageLimit = 2, UsedCount = 2
        };

        promo.IsApplicable(10000m, DateTime.UtcNow, out var message).Should().BeFalse();
        message.Should().Contain("исчерпан");
    }

    [Theory]
    [InlineData("Keychron K8 Pro", "keychron-k8-pro")]
    [InlineData("Мышь Logitech G502", "mysh-logitech-g502")]
    [InlineData("SSD 990 PRO 1 ТБ", "ssd-990-pro-1-tb")]
    public void Slug_формируется_с_транслитерацией(string source, string expected) =>
        Slug.From(source).Should().Be(expected);
}

/// <summary>Проверки складского учёта и отзывов.</summary>
public class StockAndReviewTests
{
    [Fact]
    public async Task Движение_склада_в_минус_ниже_нуля_отклоняется()
    {
        using var harness = new TestHarness();
        harness.CurrentUser.Role = UserRole.Manager;

        var act = () => harness.CreateAdminCatalogService()
            .ApplyStockMovementAsync(harness.Mouse.Id, new StockMovementRequest(-10, StockMovementReason.WriteOff, "бой"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Приход_увеличивает_остаток_и_пишет_движение()
    {
        using var harness = new TestHarness();
        harness.CurrentUser.Role = UserRole.Manager;

        var result = await harness.CreateAdminCatalogService()
            .ApplyStockMovementAsync(harness.Mouse.Id, new StockMovementRequest(8, StockMovementReason.Purchase, "поставка 14"));

        result.StockQuantity.Should().Be(10);
        (await harness.Db.StockMovements.CountAsync(m => m.Delta == 8)).Should().Be(1);
    }

    [Fact]
    public async Task Отзыв_без_покупки_запрещён()
    {
        using var harness = new TestHarness();

        var act = () => harness.CreateReviewService()
            .CreateAsync(new CreateReviewRequest(harness.Keyboard.Id, 5, "Отлично", "Пользуюсь месяц, всё нравится"));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Отзыв_публикуется_после_модерации_и_обновляет_рейтинг()
    {
        using var harness = new TestHarness();

        harness.Db.Orders.Add(new Order
        {
            Id = 1,
            Number = "ORD-20260101-0001",
            UserId = harness.Customer.Id,
            Status = OrderStatus.Completed,
            RecipientName = "Иванов Иван",
            RecipientPhone = "+79000000000",
            ItemsTotal = 10000m,
            Total = 10000m,
            Items = { new OrderItem { ProductId = harness.Keyboard.Id, ProductName = "Keychron K8", Sku = "KB-1", UnitPrice = 10000m, Quantity = 1, LineTotal = 10000m } }
        });
        await harness.Db.SaveChangesAsync();

        var reviews = harness.CreateReviewService();
        var review = await reviews.CreateAsync(new CreateReviewRequest(harness.Keyboard.Id, 4, "Хорошо", "Тихие переключатели, удобно"));

        review.IsApproved.Should().BeFalse();
        (await harness.Db.Products.FirstAsync(p => p.Id == harness.Keyboard.Id)).RatingCount.Should().Be(0);

        await reviews.ApproveAsync(review.Id);

        var product = await harness.Db.Products.FirstAsync(p => p.Id == harness.Keyboard.Id);
        product.RatingCount.Should().Be(1);
        product.RatingAvg.Should().Be(4m);
    }

    [Fact]
    public async Task Повторный_отзыв_на_товар_запрещён()
    {
        using var harness = new TestHarness();

        harness.Db.Orders.Add(new Order
        {
            Id = 1,
            Number = "ORD-20260101-0002",
            UserId = harness.Customer.Id,
            Status = OrderStatus.Delivered,
            RecipientName = "Иванов Иван",
            RecipientPhone = "+79000000000",
            ItemsTotal = 10000m,
            Total = 10000m,
            Items = { new OrderItem { ProductId = harness.Keyboard.Id, ProductName = "Keychron K8", Sku = "KB-1", UnitPrice = 10000m, Quantity = 1, LineTotal = 10000m } }
        });
        await harness.Db.SaveChangesAsync();

        var reviews = harness.CreateReviewService();
        await reviews.CreateAsync(new CreateReviewRequest(harness.Keyboard.Id, 5, null, "Первый отзыв о товаре"));

        var act = () => reviews.CreateAsync(new CreateReviewRequest(harness.Keyboard.Id, 4, null, "Второй отзыв о товаре"));

        await act.Should().ThrowAsync<ConflictException>();
    }
}
