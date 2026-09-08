using Microsoft.AspNetCore.Mvc;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Application.Services;

namespace PeriphShop.Api.Controllers;

/// <summary>Публичный каталог: категории, бренды, поиск товаров, карточка (раздел 7.2 API).</summary>
[ApiController]
[Route("api/catalog")]
[Produces("application/json")]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    /// <summary>Дерево категорий с количеством товаров (FR-01).</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await catalog.GetCategoryTreeAsync(ct));

    [HttpGet("brands")]
    [ProducesResponseType(typeof(List<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BrandDto>>> Brands(CancellationToken ct) =>
        Ok(await catalog.GetBrandsAsync(ct));

    /// <summary>Поиск с фильтрами, сортировкой, пагинацией и фасетами (FR-03 ... FR-06).</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ProductPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductPageDto>> Products([FromQuery] ProductQuery query, CancellationToken ct) =>
        Ok(await catalog.SearchAsync(query, ct));

    /// <summary>Карточка товара по ЧПУ-адресу (FR-02).</summary>
    [HttpGet("products/{slug}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> Product(string slug, CancellationToken ct) =>
        Ok(await catalog.GetBySlugAsync(slug, ct));

    /// <summary>Похожие товары (FR-07).</summary>
    [HttpGet("products/{id:int}/similar")]
    [ProducesResponseType(typeof(List<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductListItemDto>>> Similar(int id, [FromQuery] int limit = 8, CancellationToken ct = default) =>
        Ok(await catalog.GetSimilarAsync(id, Math.Clamp(limit, 1, 20), ct));

    /// <summary>Подборки для главной страницы: хиты продаж, скидки, новинки.</summary>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(FeaturedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeaturedDto>> Featured([FromQuery] int limit = 8, CancellationToken ct = default) =>
        Ok(await catalog.GetFeaturedAsync(Math.Clamp(limit, 1, 20), ct));
}

/// <summary>Отзывы о товарах (раздел 7.3 API).</summary>
[ApiController]
[Route("api/reviews")]
[Produces("application/json")]
public class ReviewsController(ReviewService reviews) : ControllerBase
{
    /// <summary>Опубликованные отзывы о товаре.</summary>
    [HttpGet("product/{productId:int}")]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> ForProduct(
        int productId, [FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await reviews.GetForProductAsync(productId, page, ct));

    /// <summary>Создание отзыва; публикуется после модерации (FR-50, FR-51).</summary>
    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var review = await reviews.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }
}
