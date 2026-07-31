using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Dual-token authentication endpoints (SRS §7): <c>login</c> issues a JWT access
/// token plus a SHA-256-hashed refresh token; <c>refresh</c> rotates the pair;
/// <c>logout</c> revokes the user's active refresh sessions. Failures throw
/// <see cref="PollenForYouApi.Exceptions.UnauthorizedException"/> which the
/// centralized <see cref="PollenForYouApi.Middleware.GlobalExceptionHandler"/>
/// renders as a uniform <c>401</c>.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Verifies administrative credentials and issues an access + refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto dto, CancellationToken ct)
    {
        return Ok(await _authService.LoginAsync(dto, ct));
    }

    /// <summary>Rotates a refresh token and issues a new access + refresh pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto dto, CancellationToken ct)
    {
        return Ok(await _authService.RefreshAsync(dto, ct));
    }

    /// <summary>Revokes all active refresh sessions for the authenticated admin.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userId, out var id))
        {
            await _authService.LogoutAsync(id, ct);
        }

        return NoContent();
    }
}
