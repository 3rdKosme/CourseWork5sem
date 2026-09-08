using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;

namespace PeriphShop.Application.Services;

/// <summary>Очередь заказов менеджера и выгрузка в CSV (FR-60, FR-64).</summary>
public class AdminOrderService(IAppDbContext db)
{
    public async Task<PagedResult<OrderListItemDto>> SearchAsync(AdminOrderQuery query, CancellationToken ct = default)
    {
        var orders = db.Orders.AsQueryable();

        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.From is { } from) orders = orders.Where(o => o.CreatedAt >= from);
        if (query.To is { } to) orders = orders.Where(o => o.CreatedAt < to);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            orders = orders.Where(o =>
                EF.Functions.Like(o.Number, $"%{term}%") ||
                EF.Functions.Like(o.RecipientName, $"%{term}%") ||
                EF.Functions.Like(o.RecipientPhone, $"%{term}%") ||
                EF.Functions.Like(o.User.Email, $"%{term}%"));
        }

        return await orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListItemDto(
                o.Id, o.Number, o.Status.ToString(), o.Total, o.Items.Sum(i => i.Quantity),
                o.DeliveryMethod.ToString(), o.PaymentStatus.ToString(), o.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    /// <summary>FR-64: выгрузка заказов за период в CSV (разделитель «;» для корректного открытия в Excel).</summary>
    public async Task<byte[]> ExportCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var orders = db.Orders.AsQueryable();
        if (from is { } f) orders = orders.Where(o => o.CreatedAt >= f);
        if (to is { } t) orders = orders.Where(o => o.CreatedAt < t);

        var rows = await orders
            .OrderBy(o => o.CreatedAt)
            .Select(o => new
            {
                o.Number,
                o.CreatedAt,
                Status = o.Status.ToString(),
                Customer = o.User.Email,
                o.RecipientName,
                o.RecipientPhone,
                Delivery = o.DeliveryMethod.ToString(),
                o.ItemsTotal,
                o.DiscountTotal,
                o.DeliveryCost,
                o.Total,
                Items = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Номер;Дата;Статус;Покупатель;Получатель;Телефон;Доставка;Позиции;Сумма позиций;Скидка;Доставка (руб);Итого");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(';', [
                Escape(r.Number),
                r.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Escape(r.Status),
                Escape(r.Customer),
                Escape(r.RecipientName),
                Escape(r.RecipientPhone),
                Escape(r.Delivery),
                r.Items.ToString(CultureInfo.InvariantCulture),
                r.ItemsTotal.ToString("0.00", CultureInfo.InvariantCulture),
                r.DiscountTotal.ToString("0.00", CultureInfo.InvariantCulture),
                r.DeliveryCost.ToString("0.00", CultureInfo.InvariantCulture),
                r.Total.ToString("0.00", CultureInfo.InvariantCulture)
            ]));
        }

        // BOM необходим, чтобы Excel распознал UTF-8.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(string? value) =>
        value is null ? string.Empty : value.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
}
