namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/admin/products</c> (Admin / Superadmin).
/// </summary>
public record CreateProductRequestDto
{
    public int CategoryId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public decimal BasePrice { get; init; }
}
