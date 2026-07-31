namespace PollenForYouApi.DTOs;

/// <summary>
/// Catalog item data contract returned by the public catalog and admin inventory
/// endpoints. <c>BasePrice</c> is the server-side baseline price.
/// </summary>
public record ProductResponseDto
{
    public int Id { get; init; }

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public decimal BasePrice { get; init; }

    public bool IsActive { get; init; }
}
