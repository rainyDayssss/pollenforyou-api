using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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

    /// <summary>Fetches the active <c>Pending</c> queue in FIFO order (5s TanStack polling).
    /// Supports HTTP conditional requests (SRS §2.3 / AGENT.md §13): the response
    /// carries a strong ETag, and a repeat poll with a matching
    /// <c>If-None-Match</c> returns a bodyless <c>304 Not Modified</c> so idle
    /// tabs don't burn bandwidth.</summary>
    [HttpGet("orders/queue")]
    public async Task<ActionResult<PagedResult<OrderQueueDto>>> GetQueue(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        var queue = await _orderService.GetQueueAsync(page, pageSize, ct);

        // Strong ETag fingerprinting the exact page representation: page bounds,
        // total count, and each row's identity + claim state. Any change to the
        // queue (new checkout, claim, settlement, expiry) alters the hash.
        var etag = ComputeQueueEtag(queue);

        // RFC 7232 §4.1: both a 200 and a 304 carry the current validator so the
        // client can refresh its stored ETag.
        Response.GetTypedHeaders().ETag = new EntityTagHeaderValue(etag);

        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch is not null
            && ifNoneMatch.Any(t => t.Tag.Equals(etag, StringComparison.Ordinal)))
        {
            // No body is written for a 304.
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(queue);
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

    /// <summary>Deterministic fingerprint of the queue page for its ETag. Covers the
    /// page window (page/pageSize), the FIFO set size (totalItems), and each row's
    /// frozen identity plus mutable claim state — so claims, settlements, expiries,
    /// and new checkouts all invalidate the tag.</summary>
    private static string ComputeQueueEtag(PagedResult<OrderQueueDto> queue)
    {
        var fingerprint = new StringBuilder();
        fingerprint.Append(queue.Page).Append('|')
            .Append(queue.PageSize).Append('|')
            .Append(queue.TotalItems);

        foreach (var item in queue.Items)
        {
            fingerprint.Append('|').Append(item.Id).Append(':')
                .Append(item.CreatedAt.Ticks).Append(':')
                .Append(item.ClaimedByEmail ?? string.Empty);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString()));
        return Convert.ToHexString(hash)[..32];
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
