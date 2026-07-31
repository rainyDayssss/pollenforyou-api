using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// Data access for the refresh-token ledger (<c>UserRefreshTokens</c>). Only
/// SHA-256 hashes are ever stored or queried — the raw token string never
/// touches the database.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<UserRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);

    Task CreateAsync(UserRefreshToken token, CancellationToken ct);

    /// <summary>
    /// Atomically consumes a token (rotation): revokes it only if it is still
    /// active, returning <c>false</c> if a concurrent request already consumed it.
    /// </summary>
    Task<bool> RevokeAsync(UserRefreshToken token, CancellationToken ct);

    /// <summary>Revokes every active session for a user (logout / reuse detection).</summary>
    Task RevokeAllForUserAsync(int userId, CancellationToken ct);
}
