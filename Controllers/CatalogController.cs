using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Public catalog endpoints (SRS §7): serves active products to customers via
/// on-load caching. Polling this endpoint is strictly forbidden by the client
/// contract (AGENT.md §13).
/// </summary>
[ApiController]
[Route("api/public/products")]
public class CatalogController : ControllerBase
{
    private readonly IProductService _productService;

    public CatalogController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Lists active products, optionally filtered by category name
    /// (e.g. <c>?category=flowers&amp;page=1&amp;pageSize=12</c>).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> GetProducts(
        CancellationToken ct,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        return Ok(await _productService.GetPublicProductsAsync(category, page, pageSize, ct));
    }
}
