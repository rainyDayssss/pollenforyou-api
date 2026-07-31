namespace PollenForYouApi.Entities;

/// <summary>
/// Financial settlement record tied to a settled order.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string PaymentStage { get; set; } = PaymentStages.Downpayment;

    public string PaymentMethod { get; set; } = PaymentMethods.GCash;

    public decimal AmountPaid { get; set; }

    public string? TransactionReference { get; set; }

    public int VerifiedByAdminId { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;

    public ApplicationUser VerifiedBy { get; set; } = null!;
}
