namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/auth/refresh</c>. The raw refresh token string
/// is exchanged for a fresh access + refresh pair (rotation).
/// </summary>
public record RefreshRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}
