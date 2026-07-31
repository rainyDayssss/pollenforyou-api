using Microsoft.AspNetCore.Identity;

namespace PollenForYouApi.Entities;

/// <summary>
/// Admin / Superadmin account. Mapped to the standard Identity "AspNetUsers" table
/// (int keys) with soft-delete and audit columns from the refined schema.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<Order> ClaimedOrders { get; set; } = [];

    public ICollection<Order> SettledOrders { get; set; } = [];

    public ICollection<Payment> VerifiedPayments { get; set; } = [];
}
