using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

public interface ICartService
{
    Task<CartDto> GetAsync(CancellationToken ct = default);
    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct = default);
    Task<CartDto> UpdateItemAsync(int productId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(int productId, CancellationToken ct = default);
    Task<CartDto> ClearAsync(CancellationToken ct = default);
    Task MergeAnonymousCartAsync(int userId, string? anonymousId, CancellationToken ct = default);

    /// <summary>Возвращает корзину текущего субъекта вместе с позициями и товарами (для оформления заказа).</summary>
    Task<Cart> ResolveCartAsync(bool createIfMissing, CancellationToken ct = default);
}

/// <summary>Корзина покупателя и гостя (FR-20 ... FR-24).</summary>
public class CartService(IAppDbContext db, ICurrentUser currentUser, IBusinessMetrics metrics) : ICartService
{
    private const int MaxQuantityPerItem = 99;

    public async Task<CartDto> GetAsync(CancellationToken ct = default)
    {
        var cart = await ResolveCartAsync(createIfMissing: false, ct);
        return Map(cart);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct = default)
    {
        if (request.Quantity is < 1 or > MaxQuantityPerItem)
            throw new BusinessRuleException($"Количество должно быть от 1 до {MaxQuantityPerItem}");

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, ct)
                      ?? throw NotFoundException.For("Товар", request.ProductId);

        var cart = await ResolveCartAsync(createIfMissing: true, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
        var desired = (item?.Quantity ?? 0) + request.Quantity;

        // FR-24: количество ограничено и остатком, и предельным значением позиции.
        var allowed = Math.Min(Math.Min(desired, MaxQuantityPerItem), Math.Max(product.StockQuantity, 0));
        if (allowed <= 0)
            throw new ConflictException($"{product.Name}: товара нет в наличии");

        if (item is null)
        {
            item = new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = allowed };
            cart.Items.Add(item);
            db.CartItems.Add(item);
        }
        else
        {
            item.Quantity = allowed;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        metrics.CartItemAdded(request.Quantity);

        return await GetAsync(ct);
    }

    public async Task<CartDto> UpdateItemAsync(int productId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        if (request.Quantity is < 1 or > MaxQuantityPerItem)
            throw new BusinessRuleException($"Количество должно быть от 1 до {MaxQuantityPerItem}");

        var cart = await ResolveCartAsync(createIfMissing: false, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId)
                   ?? throw NotFoundException.For("Позиция корзины", productId);

        var stock = await db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).FirstAsync(ct);
        if (request.Quantity > stock)
            throw new ConflictException($"Доступно только {stock} шт.");

        item.Quantity = request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetAsync(ct);
    }

    public async Task<CartDto> RemoveItemAsync(int productId, CancellationToken ct = default)
    {
        var cart = await ResolveCartAsync(createIfMissing: false, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item is not null)
        {
            db.CartItems.Remove(item);
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(ct);
    }

    public async Task<CartDto> ClearAsync(CancellationToken ct = default)
    {
        var cart = await ResolveCartAsync(createIfMissing: false, ct);
        if (cart.Items.Count > 0)
        {
            db.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Map(cart);
    }

    /// <summary>FR-21: слияние анонимной корзины с корзиной пользователя при входе.</summary>
    public async Task MergeAnonymousCartAsync(int userId, string? anonymousId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(anonymousId)) return;

        var guestCart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId && c.UserId == null, ct);
        if (guestCart is null || guestCart.Items.Count == 0) return;

        var userCart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (userCart is null)
        {
            // Достаточно переназначить владельца — позиции уже на месте.
            guestCart.UserId = userId;
            guestCart.AnonymousId = null;
            guestCart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var stocks = await db.Products
            .Where(p => guestCart.Items.Select(i => i.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.StockQuantity, ct);

        foreach (var guestItem in guestCart.Items)
        {
            var stock = stocks.GetValueOrDefault(guestItem.ProductId, 0);
            if (stock <= 0) continue;

            var existing = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
            if (existing is null)
            {
                var item = new CartItem
                {
                    CartId = userCart.Id,
                    ProductId = guestItem.ProductId,
                    Quantity = Math.Min(guestItem.Quantity, Math.Min(stock, MaxQuantityPerItem))
                };
                userCart.Items.Add(item);
                db.CartItems.Add(item);
            }
            else
            {
                existing.Quantity = Math.Min(existing.Quantity + guestItem.Quantity,
                    Math.Min(stock, MaxQuantityPerItem));
            }
        }

        db.CartItems.RemoveRange(guestCart.Items);
        db.Carts.Remove(guestCart);
        userCart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Cart> ResolveCartAsync(bool createIfMissing, CancellationToken ct = default)
    {
        Cart? cart = null;

        if (currentUser.UserId is { } userId)
        {
            cart = await db.Carts
                .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (cart is null && createIfMissing)
            {
                cart = new Cart { UserId = userId };
                db.Carts.Add(cart);
                await db.SaveChangesAsync(ct);
            }
        }
        else if (!string.IsNullOrWhiteSpace(currentUser.AnonymousCartId))
        {
            var anonymousId = currentUser.AnonymousCartId;
            cart = await db.Carts
                .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId && c.UserId == null, ct);

            if (cart is null && createIfMissing)
            {
                cart = new Cart { AnonymousId = anonymousId };
                db.Carts.Add(cart);
                await db.SaveChangesAsync(ct);
            }
        }
        else if (createIfMissing)
        {
            throw new BusinessRuleException("Не передан идентификатор корзины (заголовок X-Cart-Id)");
        }

        return cart ?? new Cart();
    }

    /// <summary>FR-23: при выдаче цены берутся актуальные, позиции с нехваткой остатка помечаются.</summary>
    private static CartDto Map(Cart cart)
    {
        var items = cart.Items
            .Where(i => i.Product is not null)
            .OrderBy(i => i.AddedAt)
            .Select(i => new CartItemDto(
                i.ProductId,
                i.Product.Name,
                i.Product.Slug,
                i.Product.Price,
                i.Quantity,
                Math.Round(i.Product.Price * i.Quantity, 2, MidpointRounding.AwayFromZero),
                i.Product.StockQuantity,
                i.Product.Images.OrderByDescending(im => im.IsPrimary).ThenBy(im => im.SortOrder)
                    .Select(im => im.Url).FirstOrDefault(),
                i.Quantity > i.Product.StockQuantity))
            .ToList();

        return new CartDto(
            items,
            Math.Round(items.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero),
            items.Sum(i => i.Quantity),
            items.Any(i => i.HasStockIssue));
    }
}
