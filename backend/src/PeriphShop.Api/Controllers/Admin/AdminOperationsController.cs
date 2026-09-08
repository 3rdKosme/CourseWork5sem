using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeriphShop.Api.Infrastructure;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;

namespace PeriphShop.Api.Controllers.Admin;

/// <summary>Очередь заказов менеджера (раздел 9.3 API).</summary>
[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminOrdersController(AdminOrderService adminOrders, OrderService orders) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> Search([FromQuery] AdminOrderQuery query, CancellationToken ct) =>
        Ok(await adminOrders.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDetailsDto>> Get(int id, CancellationToken ct) =>
        Ok(await orders.GetByIdAsync(id, ct));

    /// <summary>Смена статуса заказа с проверкой допустимости перехода (FR-36, FR-39).</summary>
    [HttpPost("{id:int}/status")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailsDto>> ChangeStatus(int id, ChangeOrderStatusRequest request, CancellationToken ct) =>
        Ok(await orders.ChangeStatusAsync(id, request, ct));

    /// <summary>Выгрузка заказов за период в CSV (FR-64).</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var csv = await adminOrders.ExportCsvAsync(from, to, ct);
        return File(csv, "text/csv; charset=utf-8", $"orders-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }
}

/// <summary>Модерация отзывов (раздел 9.4 API).</summary>
[ApiController]
[Route("api/admin/reviews")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminReviewsController(ReviewService reviews) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> Get([FromQuery] AdminReviewQuery query, CancellationToken ct) =>
        Ok(await reviews.GetForModerationAsync(query, ct));

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        await reviews.ApproveAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await reviews.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Управление промокодами (раздел 9.5 API).</summary>
[ApiController]
[Route("api/admin/promo")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminPromoController(PromoService promo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PromoCodeDto>>> Get(CancellationToken ct) => Ok(await promo.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PromoCodeDto>> Create(PromoCodeInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await promo.CreateAsync(input, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PromoCodeDto>> Update(int id, PromoCodeInput input, CancellationToken ct) =>
        Ok(await promo.UpdateAsync(id, input, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await promo.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Пользователи и роли — только администратор (раздел 9.6 API).</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public class AdminUsersController(UserAdminService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Search([FromQuery] AdminUserQuery query, CancellationToken ct) =>
        Ok(await users.SearchAsync(query, ct));

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(int id, ChangeRoleRequest request, CancellationToken ct) =>
        Ok(await users.ChangeRoleAsync(id, request, ct));

    [HttpPost("{id:int}/toggle-active")]
    public async Task<ActionResult<AdminUserDto>> ToggleActive(int id, CancellationToken ct) =>
        Ok(await users.ToggleActiveAsync(id, ct));
}

/// <summary>Отчёты и журнал аудита (раздел 9.7 API).</summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Сводка за период: выручка, заказы, средний чек (FR-60).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SalesSummaryDto>> Summary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await reports.GetSummaryAsync(from, to, ct));

    /// <summary>Топ продаваемых товаров (FR-61).</summary>
    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int limit = 10, CancellationToken ct = default) =>
        Ok(await reports.GetTopProductsAsync(from, to, Math.Clamp(limit, 1, 50), ct));

    /// <summary>Товары, заканчивающиеся на складе (FR-63).</summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<List<LowStockDto>>> LowStock([FromQuery] int threshold = 5, CancellationToken ct = default) =>
        Ok(await reports.GetLowStockAsync(Math.Clamp(threshold, 1, 100), ct));

    /// <summary>Динамика продаж по дням.</summary>
    [HttpGet("sales-by-day")]
    public async Task<ActionResult<List<SalesByDayDto>>> SalesByDay(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await reports.GetSalesByDayAsync(from, to, ct));
}

/// <summary>Журнал аудита — только администратор (FR-71).</summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public class AdminAuditController(ReportService reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Get([FromQuery] AuditQuery query, CancellationToken ct) =>
        Ok(await reports.GetAuditAsync(query, ct));
}
