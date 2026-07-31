namespace PollenForYouApi.DTOs;

/// <summary>
/// Payment ledger record tied to a settled order.
/// </summary>
public record PaymentDto
{
    public int Id { get; init; }

    public string PaymentStage { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = string.Empty;

    public decimal AmountPaid { get; init; }

    public string? TransactionReference { get; init; }

    public DateTime PaidAt { get; init; }

    /// <summary>Raw audit FK to the verifying admin (per AGENT.md, do not project the filtered principal).</summary>
    public int VerifiedByAdminId { get; init; }
}
