namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/auth/login</c>.
/// </summary>
public record LoginRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
