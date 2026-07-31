using PollenForYouApi.DTOs;

namespace PollenForYouApi.Services;

/// <summary>
/// Domain logic for the admin order flow: live queue, workspace claims (15-minute
/// lock, 409 on collision), payment settlement, and the fulfillment state machine.
/// </summary>
public interface IOrderService
{
    Task<PagedResult<OrderQueueDto>> GetQueueAsync(int page, int pageSize, CancellationToken ct);

    Task<OrderDetailDto> ClaimOrderAsync(string orderNumber, int adminUserId, CancellationToken ct);

    Task ReleaseClaimAsync(string orderNumber, int adminUserId, CancellationToken ct);

    Task<OrderDetailDto> ConfirmSettlementAsync(OrderConfirmationRequestDto dto, int adminUserId, CancellationToken ct);

    Task<OrderDetailDto> UpdateStatusAsync(int id, OrderStatusUpdateRequestDto dto, CancellationToken ct);
}
