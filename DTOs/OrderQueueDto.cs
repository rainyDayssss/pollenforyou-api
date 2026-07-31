namespace PollenForYouApi.DTOs;

/// <summary>
/// Row shown in the admin live queue (SRS §2.3): active <c>Pending</c> orders in
/// FIFO order, so admins can spot and claim them.
/// </summary>
public record OrderQueueDto
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

    public decimal TotalPrice { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    /// <summary>Email of the admin currently holding the workspace claim, if any.</summary>
    public string? ClaimedByEmail { get; init; }
}
