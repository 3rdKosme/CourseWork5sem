using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeriphShop.Api.Infrastructure;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;

namespace PeriphShop.Api.Controllers.Admin;

/// <summary>Управление товарами и складом (раздел 9.1 API). Доступ: Manager, Admin.</summary>
[ApiController]
[Route("api/admin/products")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminProductsController(AdminCatalogService catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminProductDto>>> Search([FromQuery] AdminProductQuery query, CancellationToken ct) =>
        Ok(await catalog.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminProductDto>> Get(int id, CancellationToken ct) =>
        Ok(await catalog.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(AdminProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminProductDto>> Create(ProductInput input, CancellationToken ct)
    {
        var product = await catalog.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminProductDto>> Update(int id, ProductInput input, CancellationToken ct) =>
        Ok(await catalog.UpdateAsync(id, input, ct));

    /// <summary>Мягкое удаление: товар деактивируется, история заказов сохраняется.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await catalog.DeactivateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Приход, списание или корректировка остатка (FR-45 ... FR-47).</summary>
    [HttpPost("{id:int}/stock")]
    [ProducesResponseType(typeof(AdminProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminProductDto>> Stock(int id, StockMovementRequest request, CancellationToken ct) =>
        Ok(await catalog.ApplyStockMovementAsync(id, request, ct));

    /// <summary>История движений остатка по товару.</summary>
    [HttpGet("{id:int}/stock")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> StockHistory(
        int id, [FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await catalog.GetStockMovementsAsync(id, page, ct));
}

/// <summary>Справочник категорий (раздел 9.2 API).</summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminCategoriesController(AdminCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> Get(CancellationToken ct) =>
        Ok(await catalog.GetCategoriesFlatAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateCategoryAsync(input, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryInput input, CancellationToken ct) =>
        Ok(await catalog.UpdateCategoryAsync(id, input, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteCategoryAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Справочник брендов (раздел 9.2 API).</summary>
[ApiController]
[Route("api/admin/brands")]
[Authorize(Policy = Policies.Staff)]
[Produces("application/json")]
public class AdminBrandsController(AdminCatalogService catalog, CatalogService publicCatalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> Get(CancellationToken ct) =>
        Ok(await publicCatalog.GetBrandsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<BrandDto>> Create(BrandInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateBrandAsync(input, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BrandDto>> Update(int id, BrandInput input, CancellationToken ct) =>
        Ok(await catalog.UpdateBrandAsync(id, input, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalog.DeleteBrandAsync(id, ct);
        return NoContent();
    }
}
