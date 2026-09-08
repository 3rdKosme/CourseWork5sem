using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Избранные товары покупателя.</summary>
public class FavoriteService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<List<FavoriteDto>> GetAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();

        return await db.Favorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FavoriteDto(
                f.ProductId, f.Product.Name, f.Product.Slug, f.Product.Price,
                f.Product.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                    .Select(i => i.Url).FirstOrDefault(),
                f.Product.StockQuantity > 0))
            .ToListAsync(ct);
    }

    public async Task AddAsync(int productId, CancellationToken ct = default)
    {
        var userId = RequireUser();

        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            throw NotFoundException.For("Товар", productId);

        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.ProductId == productId, ct))
            return;

        db.Favorites.Add(new Favorite { UserId = userId, ProductId = productId });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int productId, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var favorite = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        if (favorite is null) return;

        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(ct);
    }

    private int RequireUser() =>
        currentUser.UserId ?? throw new UnauthorizedAppException("Требуется аутентификация");
}
