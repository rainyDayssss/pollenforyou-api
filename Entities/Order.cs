namespace PollenForYouApi.Entities;

/// <summary>
/// Unified single-ledger order record. Transient checkouts and confirmed sales live
/// in this one table, driven by the order state machine (Pending → In Production →
/// Ready for Dispatch → Dispatched → Completed / Expired / Cancelled).
/// </summary>
public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    // Customer intake & logistics snapshot
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerMessengerUsername { get; set; } = string.Empty;

    public string ReceiverName { get; set; } = string.Empty;

    public string ReceiverContactNumber { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public DateOnly DeliveryDate { get; set; }

    public TimeOnly BookingTime { get; set; }

    public string? MessageOnCard { get; set; }

    // State machine & financials
    public string Status { get; set; } = OrderStatuses.Pending;

    public decimal TotalPrice { get; set; }

    // Concurrency, workspace claim & lazy eviction
    public int? ClaimedByUserId { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int? SettledByAdminId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser? ClaimedBy { get; set; }

    public ApplicationUser? SettledBy { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];
}
