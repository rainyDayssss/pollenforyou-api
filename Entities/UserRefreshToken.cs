namespace PollenForYouApi.Entities;

/// <summary>
/// Refresh-token session record. Only the SHA-256 hash of the token string is stored.
/// </summary>
public class UserRefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
