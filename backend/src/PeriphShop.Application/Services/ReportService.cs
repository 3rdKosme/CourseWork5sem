using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Application.Services;

/// <summary>Аналитические отчёты рабочего места менеджера (FR-60 ... FR-63) и журнал аудита (FR-71).</summary>
public class ReportService(IAppDbContext db, IBusinessMetrics metrics)
{
    /// <summary>Статусы, исключаемые из выручки.</summary>
    private static readonly OrderStatus[] NonRevenueStatuses = [OrderStatus.Cancelled, OrderStatus.Refunded];

    public async Task<SalesSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (start, end) = Normalize(from, to);

        var periodOrders = db.Orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end);
        var revenueOrders = periodOrders.Where(o => !NonRevenueStatuses.Contains(o.Status));

        var ordersCount = await revenueOrders.CountAsync(ct);
        var revenue = ordersCount == 0 ? 0m : await revenueOrders.SumAsync(o => o.Total, ct);
        var itemsSold = await db.OrderItems
            .Where(i => i.Order.CreatedAt >= start && i.Order.CreatedAt < end && !NonRevenueStatuses.Contains(i.Order.Status))
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        var byStatus = await periodOrders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var newCustomers = await db.Users.CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end, ct);

        return new SalesSummaryDto(
            start, end, ordersCount, Math.Round(revenue, 2, MidpointRounding.AwayFromZero),
            ordersCount == 0 ? 0m : Math.Round(revenue / ordersCount, 2, MidpointRounding.AwayFromZero),
            newCustomers, itemsSold,
            byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count));
    }

    public async Task<List<TopProductDto>> GetTopProductsAsync(DateTime? from, DateTime? to, int limit = 10, CancellationToken ct = default)
    {
        var (start, end) = Normalize(from, to);

        return await db.OrderItems
            .Where(i => i.Order.CreatedAt >= start && i.Order.CreatedAt < end && !NonRevenueStatuses.Contains(i.Order.Status))
            .GroupBy(i => new { i.ProductId, i.ProductName, i.Sku })
            .Select(g => new TopProductDto(g.Key.ProductId, g.Key.ProductName, g.Key.Sku,
                g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(x => x.Quantity)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>FR-48, FR-63: товары с остатком ниже порога. Побочно обновляет метрики Prometheus.</summary>
    public async Task<List<LowStockDto>> GetLowStockAsync(int threshold = 5, CancellationToken ct = default)
    {
        var items = await db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity).ThenBy(p => p.Name)
            .Select(p => new LowStockDto(p.Id, p.Sku, p.Name, p.StockQuantity, p.IsActive))
            .ToListAsync(ct);

        var activeProducts = await db.Products.CountAsync(p => p.IsActive, ct);
        metrics.SetCatalogGauges(activeProducts, items.Count);

        return items;
    }

    public async Task<List<SalesByDayDto>> GetSalesByDayAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (start, end) = Normalize(from, to);

        var rows = await db.Orders
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end && !NonRevenueStatuses.Contains(o.Status))
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.Total) })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        return rows
            .Select(r => new SalesByDayDto(DateOnly.FromDateTime(r.Date), r.Count,
                Math.Round(r.Revenue, 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditAsync(AuditQuery query, CancellationToken ct = default)
    {
        var logs = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Entity)) logs = logs.Where(l => l.Entity == query.Entity);
        if (query.UserId is { } userId) logs = logs.Where(l => l.UserId == userId);
        if (query.From is { } from) logs = logs.Where(l => l.CreatedAt >= from);
        if (query.To is { } to) logs = logs.Where(l => l.CreatedAt < to);

        return await logs
            .OrderByDescending(l => l.Id)
            .Select(l => new AuditLogDto(l.Id, l.UserId, l.User != null ? l.User.Email : null,
                l.Action, l.Entity, l.EntityId, l.Payload, l.Ip, l.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    private static (DateTime Start, DateTime End) Normalize(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        return start >= end ? (end.AddDays(-1), end) : (start, end);
    }
}
