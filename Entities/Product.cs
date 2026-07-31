namespace PollenForYouApi.Entities;

/// <summary>
/// Catalog item. BasePrice is the ground-truth server-side baseline used to
/// recalculate checkout totals; client-submitted pricing is always discarded.
/// </summary>
public class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
