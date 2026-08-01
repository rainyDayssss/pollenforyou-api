namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/admin/categories</c> (Admin / Superadmin).
/// </summary>
public record CreateCategoryRequestDto
{
    public string Name { get; init; } = string.Empty;
}
