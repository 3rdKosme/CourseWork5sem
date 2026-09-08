using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Services;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Infrastructure.Persistence;

namespace PeriphShop.Tests;

/// <summary>Подстановка текущего пользователя для тестов.</summary>
public class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
    public string? Ip => "127.0.0.1";
    public bool IsAuthenticated => UserId.HasValue;
    public string? AnonymousCartId { get; set; }
}

/// <summary>Метрики-заглушка: тесты проверяют бизнес-логику, а не экспорт в Prometheus.</summary>
public class FakeMetrics : IBusinessMetrics
{
    public int OrdersCreated { get; private set; }
    public decimal Revenue { get; private set; }
    public List<string> Failures { get; } = [];

    public void OrderCreated(decimal total)
    {
        OrdersCreated++;
        Revenue += total;
    }

    public void OrderStatusChanged(OrderStatus from, OrderStatus to) { }
    public void CartItemAdded(int quantity) { }
    public void CheckoutFailed(string reason) => Failures.Add(reason);
    public void LoginAttempt(bool success) { }
    public void SetCatalogGauges(int activeProducts, int lowStockProducts) { }
}

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Sent.Add((to, subject));
        return Task.CompletedTask;
    }
}

public class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = [];

    public Task WriteAsync(string action, string entity, object? entityId = null, object? payload = null,
        CancellationToken ct = default)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Общая обвязка юнит-тестов: контекст EF Core InMemory с готовым каталогом и покупателем.
/// Транзакции провайдером InMemory не поддерживаются, поэтому соответствующее предупреждение подавляется.
/// </summary>
public sealed class TestHarness : IDisposable
{
    public AppDbContext Db { get; }
    public FakeCurrentUser CurrentUser { get; } = new();
    public FakeMetrics Metrics { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeAuditService Audit { get; } = new();

    public User Customer { get; }
    public Product Keyboard { get; }
    public Product Mouse { get; }

    public TestHarness()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"periphshop-tests-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new AppDbContext(options);

        var category = new Category { Id = 1, Name = "Клавиатуры", Slug = "klaviatury" };
        var brand = new Brand { Id = 1, Name = "Keychron", Slug = "keychron" };

        Customer = new User
        {
            Id = 1,
            Email = "user@periphshop.local",
            PasswordHash = "hash",
            FullName = "Иванов Иван",
            Role = UserRole.Customer
        };

        Keyboard = new Product
        {
            Id = 1, Sku = "KB-1", Name = "Keychron K8", Slug = "keychron-k8",
            Category = category, Brand = brand, Price = 10000m, StockQuantity = 5
        };

        Mouse = new Product
        {
            Id = 2, Sku = "MS-1", Name = "Logitech G502", Slug = "logitech-g502",
            Category = category, Brand = brand, Price = 5000m, StockQuantity = 2
        };

        Db.AddRange(category, brand, Customer, Keyboard, Mouse);
        Db.SaveChanges();

        CurrentUser.UserId = Customer.Id;
        CurrentUser.Role = UserRole.Customer;
    }

    public CartService CreateCartService() => new(Db, CurrentUser, Metrics);

    public OrderService CreateOrderService() =>
        new(Db, CurrentUser, CreateCartService(), Metrics, Email, Audit,
            NullLogger<OrderService>.Instance);

    public ReviewService CreateReviewService() => new(Db, CurrentUser, Audit);

    public PromoService CreatePromoService() => new(Db, Audit);

    public AdminCatalogService CreateAdminCatalogService() => new(Db, CurrentUser, Audit);

    /// <summary>Кладёт товар в корзину покупателя напрямую, без прохода через сервис.</summary>
    public async Task AddToCartAsync(Product product, int quantity)
    {
        var cart = await Db.Carts.FirstOrDefaultAsync(c => c.UserId == CurrentUser.UserId);
        if (cart is null)
        {
            cart = new Cart { UserId = CurrentUser.UserId };
            Db.Carts.Add(cart);
            await Db.SaveChangesAsync();
        }

        Db.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = quantity });
        await Db.SaveChangesAsync();
    }

    public void Dispose() => Db.Dispose();
}
