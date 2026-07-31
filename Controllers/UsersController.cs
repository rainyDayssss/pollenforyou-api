using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Services;

namespace PollenForYouApi.Controllers;

/// <summary>
/// Superadmin-exclusive administrative account management endpoints (SRS §7).
/// Errors are not formatted here — domain exceptions propagate to the centralized
/// <see cref="PollenForYouApi.Middleware.GlobalExceptionHandler"/>.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.Superadmin)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lists administrative user accounts, including soft-deleted ones.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetUsers(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        return Ok(await _userService.GetUsersAsync(page, pageSize, ct));
    }

    /// <summary>
    /// Registers a new Admin or Superadmin account.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserRequestDto dto, CancellationToken ct)
    {
        var user = await _userService.CreateUserAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>
    /// Re-activates a soft-deleted account so it can authenticate again.
    /// </summary>
    [HttpPatch("{id:int}/reactivate")]
    public async Task<ActionResult<UserResponseDto>> ReactivateUser(int id, CancellationToken ct)
    {
        return Ok(await _userService.ReactivateUserAsync(id, ct));
    }

    /// <summary>
    /// Soft-deletes an administrative account (<c>IsActive = false</c>).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        await _userService.DeleteUserAsync(id, ct);
        return NoContent();
    }
}
