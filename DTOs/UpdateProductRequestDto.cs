namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>PATCH /api/admin/products/{id}</c> (Superadmin only).
/// All fields are nullable — only the fields supplied are updated (partial/PATCH
/// semantics), including toggling the <c>IsActive</c> availability flag.
/// </summary>
public record UpdateProductRequestDto
{
    public int? CategoryId { get; init; }

    public string? ProductCode { get; init; }

    public string? Name { get; init; }

    public decimal? BasePrice { get; init; }

    public bool? IsActive { get; init; }
}
