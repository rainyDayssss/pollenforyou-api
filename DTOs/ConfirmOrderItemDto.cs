namespace PollenForYouApi.DTOs;

/// <summary>
/// A single confirmed line item on settlement. The server resolves the frozen
/// product name and purchase price from the database — client pricing is discarded.
/// </summary>
public record ConfirmOrderItemDto
{
    public int ProductId { get; init; }

    public int Quantity { get; init; }
}
