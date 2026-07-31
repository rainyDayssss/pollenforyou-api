using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Data;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// EF Core data access for the catalog. Public reads are non-tracking and rely on
/// the global <c>IsActive</c> filter; admin reads bypass it. Partial PATCH updates
/// use a single <c>COALESCE</c>-based <c>ExecuteUpdateAsync</c> (no change tracker).
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly PfyDbContext _db;
    private readonly IMapper _mapper;

    public ProductRepository(PfyDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProductResponseDto>> GetProductsPageAsync(
        string? category, int page, int pageSize, bool includeDeleted, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking();
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalized = category.ToLower();
            query = query.Where(p => p.Category.Name.ToLower() == normalized);
        }

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<ProductResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PagedResult<ProductResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    public async Task<bool> CategoryExistsAsync(int categoryId, CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == categoryId, ct);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByProductCodeAsync(string productCode, int? excludeId, CancellationToken ct)
    {
        return await _db.Products
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.ProductCode == productCode && (!excludeId.HasValue || p.Id != excludeId.Value), ct);
    }

    public async Task<ProductResponseDto> CreateAsync(Product product, CancellationToken ct)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == product.Id)
            .ProjectTo<ProductResponseDto>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task<ProductResponseDto?> UpdateAsync(int id, UpdateProductRequestDto dto, CancellationToken ct)
    {
        // COALESCE pattern: each supplied field overrides the row value; omitted
        // (null) fields keep their current value — proper PATCH semantics in one
        // set-based statement.
        var affected = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Name, p => dto.Name ?? p.Name)
                .SetProperty(p => p.ProductCode, p => dto.ProductCode ?? p.ProductCode)
                .SetProperty(p => p.CategoryId, p => dto.CategoryId ?? p.CategoryId)
                .SetProperty(p => p.BasePrice, p => dto.BasePrice ?? p.BasePrice)
                .SetProperty(p => p.IsActive, p => dto.IsActive ?? p.IsActive), ct);

        if (affected == 0)
        {
            return null;
        }

        return await _db.Products
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.Id == id)
            .ProjectTo<ProductResponseDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }
}
