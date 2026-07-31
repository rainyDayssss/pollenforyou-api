namespace PollenForYouApi.Options;

/// <summary>
/// Rate limiting configuration (SRS §4): a fixed-window per-IP policy guarding
/// the public checkout endpoint. Backed by ASP.NET Core's built-in
/// <c>AddRateLimiter</c> — no external packages.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>Max checkout submissions per IP per window (default: 10).</summary>
    public int CheckoutPermitLimit { get; set; } = 10;

    /// <summary>Fixed-window length in seconds (default: 60).</summary>
    public int CheckoutWindowSeconds { get; set; } = 60;
}
