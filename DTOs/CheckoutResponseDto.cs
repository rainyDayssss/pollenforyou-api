namespace PollenForYouApi.DTOs;

/// <summary>
/// Confirmation payload returned to the customer after checkout (SRS §3.1.1):
/// the deterministic Order Number plus the server-computed total and the 2-hour
/// expiry boundary of the unverified <c>Pending</c> order.
/// </summary>
public record CheckoutResponseDto
{
    public string OrderNumber { get; init; } = string.Empty;

    public decimal TotalPrice { get; init; }

    public DateTime ExpiresAt { get; init; }
}
