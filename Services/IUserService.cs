using PollenForYouApi.DTOs;

namespace PollenForYouApi.Services;

/// <summary>
/// Domain logic for administrative account lifecycle management.
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserResponseDto>> GetUsersAsync(int page, int pageSize, CancellationToken ct);

    Task<UserResponseDto> CreateUserAsync(CreateUserRequestDto dto, CancellationToken ct);

    Task<UserResponseDto> ReactivateUserAsync(int id, CancellationToken ct);

    Task DeleteUserAsync(int id, CancellationToken ct);
}
