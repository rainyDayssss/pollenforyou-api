using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// Catalog data access. The public listing respects the <c>IsActive</c> global
/// query filter; the admin inventory bypasses it with <c>IgnoreQueryFilters()</c>.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Returns a paginated page of products, optionally filtered by a normalized
    /// category name. <paramref name="includeDeleted"/> controls whether the
    /// soft-delete query filter is bypassed (admin view).
    /// </summary>
    Task<PagedResult<ProductResponseDto>> GetProductsPageAsync(
        string? category, int page, int pageSize, bool includeDeleted, CancellationToken ct);

    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken ct);

    /// <summary>Loads the given active products (query filter applies) — used as server-side ground truth for settlement.</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>
    /// True if any product (including soft-deleted ones — they still occupy the
    /// unique <c>ProductCode</c> index) already uses the code, excluding the
    /// optional <paramref name="excludeId"/> (for updates).
    /// </summary>
    Task<bool> ExistsByProductCodeAsync(string productCode, int? excludeId, CancellationToken ct);

    Task<ProductResponseDto> CreateAsync(Product product, CancellationToken ct);

    /// <summary>Applies the supplied PATCH fields; returns the updated DTO, or <c>null</c> if not found.</summary>
    Task<ProductResponseDto?> UpdateAsync(int id, UpdateProductRequestDto dto, CancellationToken ct);
}
