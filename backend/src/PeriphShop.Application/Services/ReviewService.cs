using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Отзывы и рейтинги с модерацией (FR-50 ... FR-52).</summary>
public class ReviewService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit)
{
    private static readonly OrderStatus[] PurchaseConfirmingStatuses =
        [OrderStatus.Delivered, OrderStatus.Completed];

    public async Task<PagedResult<ReviewDto>> GetForProductAsync(int productId, PageRequest page, CancellationToken ct = default) =>
        await db.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.Product.Name, r.User.FullName, r.Rating,
                r.Title, r.Body, r.IsApproved, r.CreatedAt))
            .ToPagedResultAsync(page, ct);

    public async Task<ReviewDto> CreateAsync(CreateReviewRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Требуется аутентификация");

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
                      ?? throw NotFoundException.For("Товар", request.ProductId);

        if (request.Rating is < 1 or > 5)
            throw new BusinessRuleException("Оценка должна быть от 1 до 5");

        // FR-50: отзыв доступен только покупателю с завершённым заказом этого товара.
        var hasPurchase = await db.Orders.AnyAsync(o =>
            o.UserId == userId &&
            PurchaseConfirmingStatuses.Contains(o.Status) &&
            o.Items.Any(i => i.ProductId == request.ProductId), ct);
        if (!hasPurchase)
            throw new ForbiddenException("Отзыв можно оставить только на полученный товар");

        if (await db.Reviews.AnyAsync(r => r.ProductId == request.ProductId && r.UserId == userId, ct))
            throw new ConflictException("Вы уже оставили отзыв на этот товар");

        var review = new Review
        {
            ProductId = request.ProductId,
            UserId = userId,
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Body = request.Body.Trim(),
            IsApproved = false
        };

        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ReviewCreated", nameof(Review), review.Id, new { review.ProductId, review.Rating }, ct);

        var authorName = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync(ct);
        return new ReviewDto(review.Id, review.ProductId, product.Name, authorName, review.Rating,
            review.Title, review.Body, review.IsApproved, review.CreatedAt);
    }

    // ---------- Модерация (Manager/Admin) ----------

    public async Task<PagedResult<ReviewDto>> GetForModerationAsync(AdminReviewQuery query, CancellationToken ct = default)
    {
        var reviews = db.Reviews.AsQueryable();
        if (query.Approved is { } approved) reviews = reviews.Where(r => r.IsApproved == approved);

        return await reviews
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.Product.Name, r.User.FullName, r.Rating,
                r.Title, r.Body, r.IsApproved, r.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task ApproveAsync(int id, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw NotFoundException.For("Отзыв", id);

        if (!review.IsApproved)
        {
            review.IsApproved = true;
            await db.SaveChangesAsync(ct);
            await RecalculateRatingAsync(review.ProductId, ct);
            await audit.WriteAsync("ReviewApproved", nameof(Review), id, null, ct);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw NotFoundException.For("Отзыв", id);

        var productId = review.ProductId;
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(productId, ct);
        await audit.WriteAsync("ReviewDeleted", nameof(Review), id, null, ct);
    }

    /// <summary>FR-52: пересчёт денормализованного рейтинга товара по опубликованным отзывам.</summary>
    private async Task RecalculateRatingAsync(int productId, CancellationToken ct)
    {
        var stats = await db.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .GroupBy(_ => 1)
            .Select(g => new { Avg = g.Average(r => (decimal)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return;

        product.RatingAvg = stats is null ? 0m : Math.Round(stats.Avg, 2, MidpointRounding.AwayFromZero);
        product.RatingCount = stats?.Count ?? 0;
        await db.SaveChangesAsync(ct);
    }
}
