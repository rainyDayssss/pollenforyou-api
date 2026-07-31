namespace PollenForYouApi.DTOs;

/// <summary>
/// Payment details submitted with an order settlement confirmation.
/// </summary>
public record PaymentRequestDto
{
    public string PaymentStage { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = string.Empty;

    public decimal AmountPaid { get; init; }

    public string? TransactionReference { get; init; }
}
