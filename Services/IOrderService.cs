using PollenForYouApi.DTOs;

namespace PollenForYouApi.Services;

/// <summary>
/// Domain logic for the admin order flow: live queue, workspace claims (15-minute
/// lock, 409 on collision), payment settlement, and the fulfillment state machine.
/// </summary>
public interface IOrderService
{
    /// <summary>Public checkout (SRS §3.1.1): lazy eviction hook, server-side total
    /// recompute from DB base prices, then a <c>Pending</c> order with a 2-hour TTL.
    /// When <paramref name="idempotencyKey"/> is supplied, replays resolve to the
    /// original order instead of creating a duplicate.</summary>
    Task<CheckoutResponseDto> SubmitCheckoutAsync(CheckoutRequestDto dto, string? idempotencyKey, CancellationToken ct);

    Task<PagedResult<OrderQueueDto>> GetQueueAsync(int page, int pageSize, CancellationToken ct);

    Task<OrderDetailDto> ClaimOrderAsync(string orderNumber, int adminUserId, CancellationToken ct);

    Task ReleaseClaimAsync(string orderNumber, int adminUserId, CancellationToken ct);

    Task<OrderDetailDto> ConfirmSettlementAsync(OrderConfirmationRequestDto dto, int adminUserId, CancellationToken ct);

    Task<OrderDetailDto> UpdateStatusAsync(int id, OrderStatusUpdateRequestDto dto, CancellationToken ct);
}
