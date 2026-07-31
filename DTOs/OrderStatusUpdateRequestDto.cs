namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for <c>PATCH /api/admin/orders/{id}/status</c>: the target
/// fulfillment state machine state.
/// </summary>
public record OrderStatusUpdateRequestDto
{
    public string Status { get; init; } = string.Empty;
}
