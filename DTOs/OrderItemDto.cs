namespace PollenForYouApi.DTOs;

/// <summary>
/// Settled order line item with frozen snapshot fields (immutable audit trail).
/// </summary>
public record OrderItemDto
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public string ProductNameSnapshot { get; init; } = string.Empty;

    public decimal PriceAtPurchase { get; init; }

    public int Quantity { get; init; }
}
