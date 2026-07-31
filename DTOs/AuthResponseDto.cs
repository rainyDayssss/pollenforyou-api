namespace PollenForYouApi.DTOs;

/// <summary>
/// Successful authentication payload: a signed JWT access token plus the raw
/// refresh token that the client stores to obtain future pairs.
/// </summary>
public record AuthResponseDto
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public int ExpiresInSeconds { get; init; }

    public string Email { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];
}
