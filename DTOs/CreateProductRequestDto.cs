namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/admin/products</c> (Superadmin only).
/// </summary>
public record CreateProductRequestDto
{
    public int CategoryId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal BasePrice { get; init; }
}
