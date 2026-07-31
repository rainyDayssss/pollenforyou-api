using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PollenForYouApi.DTOs;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Public customer checkout (SRS §3.1.1 / §7): submits delivery details + cart
/// lines, returns the deterministic Order Number. Guarded by the built-in
/// ASP.NET Core rate limiter (fixed-window per IP — SRS §4).
/// </summary>
[ApiController]
[Route("api/public/checkout")]
[EnableRateLimiting("checkout")]
public class CheckoutController : ControllerBase
{
    private readonly IOrderService _orderService;

    public CheckoutController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Submits a checkout; returns the Order Number confirmation. The optional
    /// <c>Idempotency-Key</c> header makes retries safe: the same key always
    /// resolves to the same order (never a duplicate).
    /// </summary>
    [HttpPost("submit")]
    [AllowAnonymous]
    public async Task<ActionResult<CheckoutResponseDto>> Submit(
        CheckoutRequestDto dto,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        return Ok(await _orderService.SubmitCheckoutAsync(dto, idempotencyKey, ct));
    }
}
