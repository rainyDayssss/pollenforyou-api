using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Admin category endpoints (catalog grouping): listing and creation so the admin
/// UI can manage categories directly instead of deriving them from existing
/// products. Category domain logic lives in <see cref="IProductService"/> (the
/// catalog service), matching where <c>CategoryExistsAsync</c> already lives.
/// Errors are rendered by the centralized
/// <see cref="PollenForYouApi.Middleware.GlobalExceptionHandler"/>.
/// </summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Superadmin}")]
public class CategoriesController : ControllerBase
{
    private readonly IProductService _productService;

    public CategoriesController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Lists all categories ordered by name — including soft-deactivated ones,
    /// mirroring the admin product listing (<c>IgnoreQueryFilters()</c>).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken ct)
    {
        return Ok(await _productService.GetCategoriesAsync(ct));
    }

    /// <summary>Creates a new category; <c>409</c> if the name already exists.</summary>
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryRequestDto dto, CancellationToken ct)
    {
        var category = await _productService.CreateCategoryAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, category);
    }
}
