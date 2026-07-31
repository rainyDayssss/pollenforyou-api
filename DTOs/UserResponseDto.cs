namespace PollenForYouApi.DTOs;

/// <summary>
/// Administrative account data contract returned by the users endpoints.
/// </summary>
public record UserResponseDto
{
    public int Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }
}
