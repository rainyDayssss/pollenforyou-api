namespace PollenForYouApi.DTOs;

/// <summary>
/// Admin category data contract for <c>GET/POST /api/admin/categories</c>.
/// </summary>
public record CategoryDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
