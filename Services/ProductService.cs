using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;
using PollenForYouApi.Repositories;

namespace PollenForYouApi.Services;

/// <summary>
/// Catalog domain logic. Category names are normalized to lowercase before the
/// EF query (SRS §2.5) and <c>pageSize</c> is clamped to the 50-item hard max
/// (SRS §2.4). Duplicate product codes surface as
/// <see cref="DuplicateProductCodeException"/> → <c>409 Conflict</c>, detected
/// both by pre-check and by the DB unique index as a backstop.
/// </summary>
public class ProductService : IProductService
{
    private const int MaxPageSize = 50;

    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public ProductService(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public Task<PagedResult<ProductResponseDto>> GetPublicProductsAsync(
        string? category, int page, int pageSize, CancellationToken ct)
    {
        return GetProductsPageAsync(category, page, pageSize, includeDeleted: false, ct);
    }

    public Task<PagedResult<ProductResponseDto>> GetAdminProductsAsync(
        string? category, int page, int pageSize, CancellationToken ct)
    {
        return GetProductsPageAsync(category, page, pageSize, includeDeleted: true, ct);
    }

    public async Task<ProductResponseDto> CreateProductAsync(CreateProductRequestDto dto, CancellationToken ct)
    {
        if (!await _productRepository.CategoryExistsAsync(dto.CategoryId, ct))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(CreateProductRequestDto.CategoryId),
                    "The specified category does not exist.")
            ]);
        }

        if (await _productRepository.ExistsByProductCodeAsync(dto.ProductCode, null, ct))
        {
            throw new DuplicateProductCodeException();
        }

        var product = _mapper.Map<Product>(dto);
        product.IsActive = true;

        try
        {
            return await _productRepository.CreateAsync(product, ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateProductCodeException();
        }
    }

    public async Task<ProductResponseDto> UpdateProductAsync(int id, UpdateProductRequestDto dto, CancellationToken ct)
    {
        if (dto.CategoryId.HasValue && !await _productRepository.CategoryExistsAsync(dto.CategoryId.Value, ct))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(UpdateProductRequestDto.CategoryId),
                    "The specified category does not exist.")
            ]);
        }

        if (dto.ProductCode is not null && await _productRepository.ExistsByProductCodeAsync(dto.ProductCode, id, ct))
        {
            throw new DuplicateProductCodeException();
        }

        try
        {
            return await _productRepository.UpdateAsync(id, dto, ct)
                ?? throw new NotFoundException($"Product with id {id} was not found.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateProductCodeException();
        }
    }

    private async Task<PagedResult<ProductResponseDto>> GetProductsPageAsync(
        string? category, int page, int pageSize, bool includeDeleted, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToLowerInvariant();

        return await _productRepository.GetProductsPageAsync(normalizedCategory, page, pageSize, includeDeleted, ct);
    }

    /// <summary>
    /// Matches SQL Server unique index (2601) / unique constraint (2627) violations
    /// raised by the unique index on <c>ProductCode</c>.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
