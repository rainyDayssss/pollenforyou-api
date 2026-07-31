using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// Data access for administrative accounts. All reads bypass the <c>IsActive</c>
/// global query filter so soft-deleted accounts remain visible to superadmins.
/// </summary>
public interface IUserRepository
{
    Task<PagedResult<UserResponseDto>> GetUsersPageAsync(int page, int pageSize, CancellationToken ct);

    Task<ApplicationUser?> GetByIdIncludingDeletedAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<string>> GetRolesForUserAsync(int userId, CancellationToken ct);

    Task<bool> SetIsActiveAsync(int id, bool isActive, CancellationToken ct);
}
