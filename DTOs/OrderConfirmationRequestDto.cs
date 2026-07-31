namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>POST /api/orders/confirm</c>: the verified line items
/// (frozen snapshots resolved server-side) and the payment settlement record.
/// </summary>
public record OrderConfirmationRequestDto
{
    public string OrderNumber { get; init; } = string.Empty;

    public IReadOnlyList<ConfirmOrderItemDto> Items { get; init; } = [];

    public PaymentRequestDto? Payment { get; init; }
}
