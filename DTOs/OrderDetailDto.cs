namespace PollenForYouApi.DTOs;

/// <summary>
/// Full order record returned by the claim, settlement, and status endpoints so the
/// admin workspace renders the complete intake, financial, and fulfillment state.
/// </summary>
public record OrderDetailDto
{
    public int Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerMessengerUsername { get; init; } = string.Empty;

    public string ReceiverName { get; init; } = string.Empty;

    public string ReceiverContactNumber { get; init; } = string.Empty;

    public string DeliveryAddress { get; init; } = string.Empty;

    public DateOnly DeliveryDate { get; init; }

    public TimeOnly BookingTime { get; init; }

    public string? MessageOnCard { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal TotalPrice { get; init; }

    public string? ClaimedByEmail { get; init; }

    public DateTime? LockedUntil { get; init; }

    public int? SettledByAdminId { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];

    public IReadOnlyList<PaymentDto> Payments { get; init; } = [];
}
