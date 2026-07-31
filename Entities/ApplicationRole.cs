using Microsoft.AspNetCore.Identity;

namespace PollenForYouApi.Entities;

/// <summary>
/// Identity role ("Admin" or "Superadmin") backed by the standard AspNetRoles table.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
}
