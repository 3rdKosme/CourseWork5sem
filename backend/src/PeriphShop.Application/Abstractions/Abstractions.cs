using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Application.Abstractions;

/// <summary>
/// Контракт единицы работы для прикладного слоя: сервисы не зависят от конкретной реализации
/// EF-контекста, что позволяет подменять его в юнит-тестах (EF Core InMemory).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistory { get; }
    DbSet<PromoCode> PromoCodes { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Сведения о текущем пользователе запроса (заполняются из JWT).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    string? Ip { get; }
    bool IsAuthenticated { get; }

    /// <summary>Идентификатор анонимной корзины из заголовка X-Cart-Id (FR-20).</summary>
    string? AnonymousCartId { get; }
}

/// <summary>Хэширование и проверка паролей (BCrypt, FR-10).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt);

/// <summary>Выдача JWT и refresh-токенов (FR-12).</summary>
public interface ITokenService
{
    TokenPair Issue(User user);
}

/// <summary>Отправка почтовых уведомлений (FR-41). В стенде письма перехватывает Mailpit.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>Бизнес-метрики Prometheus (раздел 10.1 ТЗ). Реализация — в слое представления.</summary>
public interface IBusinessMetrics
{
    void OrderCreated(decimal total);
    void OrderStatusChanged(OrderStatus from, OrderStatus to);
    void CartItemAdded(int quantity);
    void CheckoutFailed(string reason);
    void LoginAttempt(bool success);
    void SetCatalogGauges(int activeProducts, int lowStockProducts);
}

/// <summary>Запись в журнал аудита (FR-70).</summary>
public interface IAuditService
{
    Task WriteAsync(string action, string entity, object? entityId = null, object? payload = null,
        CancellationToken ct = default);
}
