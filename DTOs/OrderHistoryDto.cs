namespace PollenForYouApi.DTOs;

/// <summary>
/// Row in the admin order-history list: every ledger order (any status), newest
/// first, with an optional status filter. Kept lean so settled/fulfilled/expired
/// orders stay browsable without a full detail round-trip.
/// </summary>
public record OrderHistoryDto
{
    public int Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string ReceiverName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal TotalPrice { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>Email of the admin holding the workspace claim, if any (only meaningful for Pending).</summary>
    public string? ClaimedByEmail { get; init; }

    public int? SettledByAdminId { get; init; }
}
