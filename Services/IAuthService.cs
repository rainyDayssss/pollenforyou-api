using PollenForYouApi.DTOs;

namespace PollenForYouApi.Services;

/// <summary>
/// Domain logic for the dual-token authentication lifecycle: credential
/// verification, refresh-token rotation, and session invalidation.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct);

    Task<AuthResponseDto> RefreshAsync(RefreshRequestDto dto, CancellationToken ct);

    Task LogoutAsync(int userId, CancellationToken ct);
}
