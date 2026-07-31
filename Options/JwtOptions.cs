namespace PollenForYouApi.Options;

/// <summary>
/// Strongly-typed settings bound to the <c>Jwt</c> configuration section.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Access-token validity window (short-lived; paired with refresh rotation).</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>Refresh-token session validity window after which the session expires.</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
