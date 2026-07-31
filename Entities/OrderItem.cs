namespace PollenForYouApi.Entities;

/// <summary>
/// Line item on a settled order. ProductNameSnapshot and PriceAtPurchase are frozen
/// at checkout time to guarantee financial auditability.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;

    public decimal PriceAtPurchase { get; set; }

    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
