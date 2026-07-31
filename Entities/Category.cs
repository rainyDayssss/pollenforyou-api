namespace PollenForYouApi.Entities;

/// <summary>
/// Product grouping (e.g., flowers, coffee). Seasonal groups can be soft-deactivated.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
