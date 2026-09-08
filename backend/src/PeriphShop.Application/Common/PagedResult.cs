using Microsoft.EntityFrameworkCore;

namespace PeriphShop.Application.Common;

/// <summary>Постраничный результат выборки (NFR-02: списки всегда ограничены страницей).</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public static PagedResult<T> Empty(int page, int pageSize) => new() { Page = page, PageSize = pageSize };
}

/// <summary>Базовые параметры постраничного запроса.</summary>
public class PageRequest
{
    private const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 12;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 12,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => (Page - 1) * PageSize;
}

public static class QueryablePagingExtensions
{
    /// <summary>Материализует страницу вместе с общим количеством записей.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageRequest request, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = total
        };
    }

    /// <summary>Проекция уже материализованной страницы в другой тип.</summary>
    public static PagedResult<TOut> Map<TIn, TOut>(this PagedResult<TIn> source, Func<TIn, TOut> selector) => new()
    {
        Items = source.Items.Select(selector).ToList(),
        Page = source.Page,
        PageSize = source.PageSize,
        TotalItems = source.TotalItems
    };
}
