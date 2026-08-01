using PollenForYouApi.DTOs;

namespace PollenForYouApi.Services;

/// <summary>
/// Domain logic for the catalog: public/admin listing with category filtering,
/// and Superadmin create/update with uniqueness and category-existence rules.
/// </summary>
public interface IProductService
{
    Task<PagedResult<ProductResponseDto>> GetPublicProductsAsync(string? category, int page, int pageSize, CancellationToken ct);

    Task<PagedResult<ProductResponseDto>> GetAdminProductsAsync(string? category, int page, int pageSize, CancellationToken ct);

    Task<ProductResponseDto> CreateProductAsync(CreateProductRequestDto dto, CancellationToken ct);

    Task<ProductResponseDto> UpdateProductAsync(int id, UpdateProductRequestDto dto, CancellationToken ct);

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct);

    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequestDto dto, CancellationToken ct);
}
