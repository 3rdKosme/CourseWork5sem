using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;

namespace PeriphShop.Api.Controllers;

/// <summary>
/// Корзина покупателя и гостя (раздел 7.4 API).
/// Гость передаёт идентификатор корзины в заголовке <c>X-Cart-Id</c>.
/// </summary>
[ApiController]
[Route("api/cart")]
[Produces("application/json")]
public class CartController(ICartService cart, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) => Ok(await cart.GetAsync(ct));

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> Add(AddCartItemRequest request, CancellationToken ct) =>
        Ok(await cart.AddItemAsync(request, ct));

    [HttpPut("items/{productId:int}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Update(int productId, UpdateCartItemRequest request, CancellationToken ct) =>
        Ok(await cart.UpdateItemAsync(productId, request, ct));

    [HttpDelete("items/{productId:int}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Remove(int productId, CancellationToken ct) =>
        Ok(await cart.RemoveItemAsync(productId, ct));

    [HttpDelete]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Clear(CancellationToken ct) => Ok(await cart.ClearAsync(ct));

    /// <summary>Слияние анонимной корзины с корзиной пользователя (FR-21).</summary>
    [HttpPost("merge")]
    [Authorize]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Merge(CancellationToken ct)
    {
        await cart.MergeAnonymousCartAsync(currentUser.UserId!.Value, currentUser.AnonymousCartId, ct);
        return Ok(await cart.GetAsync(ct));
    }
}

/// <summary>Проверка промокодов покупателем (раздел 7.5 API).</summary>
[ApiController]
[Route("api/promo")]
[Authorize]
[Produces("application/json")]
public class PromoController(PromoService promo) : ControllerBase
{
    [HttpPost("validate")]
    [ProducesResponseType(typeof(PromoValidationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PromoValidationDto>> Validate(ValidatePromoRequest request, CancellationToken ct) =>
        Ok(await promo.ValidateAsync(request, ct));
}

/// <summary>Избранные товары (раздел 7.7 API).</summary>
[ApiController]
[Route("api/favorites")]
[Authorize]
[Produces("application/json")]
public class FavoritesController(FavoriteService favorites) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<FavoriteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FavoriteDto>>> Get(CancellationToken ct) => Ok(await favorites.GetAsync(ct));

    [HttpPost("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Add(int productId, CancellationToken ct)
    {
        await favorites.AddAsync(productId, ct);
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        await favorites.RemoveAsync(productId, ct);
        return NoContent();
    }
}
