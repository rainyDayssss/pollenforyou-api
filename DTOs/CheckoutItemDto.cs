namespace PollenForYouApi.DTOs;

/// <summary>
/// A single cart line submitted at checkout. The server resolves the real price
/// from the database and recomputes the total — client-submitted pricing is
/// always discarded (AGENT.md §5).
/// </summary>
public record CheckoutItemDto
{
    public int ProductId { get; init; }

    public int Quantity { get; init; }
}
