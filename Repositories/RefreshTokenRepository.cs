using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Data;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// EF Core data access for <c>UserRefreshTokens</c>. Reads are non-tracking;
/// revocations use <c>ExecuteUpdateAsync</c> so no change tracker is needed.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly PfyDbContext _db;

    public RefreshTokenRepository(PfyDbContext db)
    {
        _db = db;
    }

    public async Task<UserRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        return await _db.UserRefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task CreateAsync(UserRefreshToken token, CancellationToken ct)
    {
        _db.UserRefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> RevokeAsync(UserRefreshToken token, CancellationToken ct)
    {
        // Guard on !IsRevoked so two concurrent refreshes presenting the same token
        // cannot both consume it; the loser is treated as token reuse.
        var updated = await _db.UserRefreshTokens
            .Where(t => t.Id == token.Id && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true), ct);

        return updated > 0;
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct)
    {
        await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true), ct);
    }
}
