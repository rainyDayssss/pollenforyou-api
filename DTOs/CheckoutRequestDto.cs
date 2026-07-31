namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/public/checkout/submit</c> (SRS §3.1.1).
/// Customer intake fields plus the cart lines used for server-side total
/// recomputation. Messenger username case is preserved verbatim.
/// </summary>
public record CheckoutRequestDto
{
    public string CustomerName { get; init; } = string.Empty;

    public string CustomerMessengerUsername { get; init; } = string.Empty;

    public string ReceiverName { get; init; } = string.Empty;

    public string ReceiverContactNumber { get; init; } = string.Empty;

    public string DeliveryAddress { get; init; } = string.Empty;

    public DateOnly DeliveryDate { get; init; }

    public TimeOnly BookingTime { get; init; }

    public string? MessageOnCard { get; init; }

    public IReadOnlyList<CheckoutItemDto> Items { get; init; } = [];
}
