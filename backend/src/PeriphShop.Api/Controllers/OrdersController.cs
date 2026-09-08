using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;

namespace PeriphShop.Api.Controllers;

/// <summary>Заказы покупателя (раздел 7.6 API).</summary>
[ApiController]
[Route("api/orders")]
[Authorize]
[Produces("application/json")]
public class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>Оформление заказа из корзины (FR-30 ... FR-35).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDetailsDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>Список собственных заказов.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> My([FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await orders.GetMyOrdersAsync(page, ct));

    /// <summary>Детали заказа с историей статусов.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDetailsDto>> Get(int id, CancellationToken ct) =>
        Ok(await orders.GetByIdAsync(id, ct));

    /// <summary>Отмена заказа покупателем (FR-37, FR-38).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailsDto>> Cancel(int id, CancelOrderRequest request, CancellationToken ct) =>
        Ok(await orders.CancelAsync(id, request, ct));

    /// <summary>Имитация онлайн-оплаты картой (FR-40).</summary>
    [HttpPost("{id:int}/pay")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDetailsDto>> Pay(int id, CancellationToken ct) =>
        Ok(await orders.PayAsync(id, ct));
}
