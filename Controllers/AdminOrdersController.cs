using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Admin order flow endpoints (SRS §7): live queue polling, 15-minute workspace
/// claims (409 on RowVersion collision), payment settlement, and fulfillment
/// status transitions. Errors are rendered by the centralized
/// <see cref="PollenForYouApi.Middleware.GlobalExceptionHandler"/>.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Superadmin}")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>Fetches the active <c>Pending</c> queue in FIFO order (5s TanStack polling).</summary>
    [HttpGet("orders/queue")]
    public async Task<ActionResult<PagedResult<OrderQueueDto>>> GetQueue(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        return Ok(await _orderService.GetQueueAsync(page, pageSize, ct));
    }

    /// <summary>Acquires the 15-minute workspace claim for an order; 409 on collision.</summary>
    [HttpPost("orders/claim/{orderNumber}")]
    public async Task<ActionResult<OrderDetailDto>> ClaimOrder(string orderNumber, CancellationToken ct)
    {
        return Ok(await _orderService.ClaimOrderAsync(orderNumber, GetAdminUserId(), ct));
    }

    /// <summary>Releases the workspace claim held by the current admin.</summary>
    [HttpDelete("orders/claim/{orderNumber}")]
    public async Task<IActionResult> ReleaseClaim(string orderNumber, CancellationToken ct)
    {
        await _orderService.ReleaseClaimAsync(orderNumber, GetAdminUserId(), ct);
        return NoContent();
    }

    /// <summary>Verifies payment and promotes the order to <c>In Production</c> inside one atomic transaction.</summary>
    [HttpPost("orders/confirm")]
    public async Task<ActionResult<OrderDetailDto>> ConfirmSettlement(OrderConfirmationRequestDto dto, CancellationToken ct)
    {
        return Ok(await _orderService.ConfirmSettlementAsync(dto, GetAdminUserId(), ct));
    }

    /// <summary>Advances the fulfillment state machine (Ready for Dispatch, Dispatched, Completed, Cancelled).</summary>
    [HttpPatch("admin/orders/{id:int}/status")]
    public async Task<ActionResult<OrderDetailDto>> UpdateStatus(
        int id, OrderStatusUpdateRequestDto dto, CancellationToken ct)
    {
        return Ok(await _orderService.UpdateStatusAsync(id, dto, ct));
    }

    private int GetAdminUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userId, out var id))
        {
            throw new UnauthorizedException("Unable to identify the current admin.");
        }

        return id;
    }
}
