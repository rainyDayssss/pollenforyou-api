using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Admin inventory endpoints (SRS §7, with catalog mutation widened to Admin by
/// product decision). Listing, create, and update/toggle are all available to
/// Admin / Superadmin. Errors are rendered by the centralized
/// <see cref="PollenForYouApi.Middleware.GlobalExceptionHandler"/>.
/// </summary>
[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Superadmin}")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Lists inventory including soft-deleted items (via <c>IgnoreQueryFilters()</c>).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> GetProducts(
        CancellationToken ct,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        return Ok(await _productService.GetAdminProductsAsync(category, page, pageSize, ct));
    }

    /// <summary>Creates a new catalog item.</summary>
    [HttpPost]
    public async Task<ActionResult<ProductResponseDto>> CreateProduct(CreateProductRequestDto dto, CancellationToken ct)
    {
        var product = await _productService.CreateProductAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, product);
    }

    /// <summary>Partially updates a catalog item or toggles its active availability flag.</summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> UpdateProduct(
        int id, UpdateProductRequestDto dto, CancellationToken ct)
    {
        return Ok(await _productService.UpdateProductAsync(id, dto, ct));
    }
}
