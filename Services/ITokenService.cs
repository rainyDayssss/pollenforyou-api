using PollenForYouApi.Entities;

namespace PollenForYouApi.Services;

/// <summary>
/// Dual-token factory: signed JWT access tokens carrying Identity role claims
/// (so <c>[Authorize(Roles=...)]</c> actually enforces) plus opaque refresh
/// tokens whose SHA-256 hashes are stored in <c>UserRefreshTokens</c>.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed access token for the user, embedding role claims and a
    /// unique <c>jti</c>. Returns the token string, its lifetime in seconds, and
    /// the roles embedded (reused for the auth response).
    /// </summary>
    Task<(string Token, int ExpiresInSeconds, IReadOnlyList<string> Roles)> CreateAccessTokenAsync(
        ApplicationUser user, CancellationToken ct);

    /// <summary>Generates an opaque refresh token, persists its SHA-256 hash, and returns the raw token string.</summary>
    Task<string> CreateRefreshTokenSessionAsync(int userId, CancellationToken ct);

    /// <summary>SHA-256 hex digest of a refresh token string — the only representation ever stored.</summary>
    string HashToken(string token);
}
