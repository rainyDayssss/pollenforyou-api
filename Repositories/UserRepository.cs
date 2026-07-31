using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Data;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// User account data access. The superadmin users endpoints must see soft-deleted
/// accounts, so every query uses <c>IgnoreQueryFilters()</c>; the <c>IsActive</c>
/// column is surfaced on the DTO for the UI to render account status.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly PfyDbContext _db;
    private readonly IMapper _mapper;

    public UserRepository(PfyDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserResponseDto>> GetUsersPageAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(u => u.CreatedAt);

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<UserResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        items = await PopulateRolesAsync(items, ct);

        return new PagedResult<UserResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    public async Task<ApplicationUser?> GetByIdIncludingDeletedAsync(int id, CancellationToken ct)
    {
        return await _db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<IReadOnlyList<string>> GetRolesForUserAsync(int userId, CancellationToken ct)
    {
        return await (from ur in _db.UserRoles
                      join r in _db.Roles on ur.RoleId equals r.Id
                      where ur.UserId == userId
                      select r.Name!)
            .ToListAsync(ct);
    }

    public async Task<bool> SetIsActiveAsync(int id, bool isActive, CancellationToken ct)
    {
        var updated = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsActive, isActive), ct);

        return updated > 0;
    }

    /// <summary>
    /// Returns a new page of DTOs with the <c>Roles</c> collection populated in a
    /// single batched query over the Identity role-join tables (avoids the N+1
    /// problem). The DTOs are immutable — each item is rebuilt via a
    /// <c>with</c> expression.
    /// </summary>
    private async Task<List<UserResponseDto>> PopulateRolesAsync(
        IReadOnlyList<UserResponseDto> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var userIds = items.Select(i => i.Id).ToArray();

        var rows = await (from ur in _db.UserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          where userIds.Contains(ur.UserId)
                          select new { ur.UserId, RoleName = r.Name! })
            .ToListAsync(ct);

        var rolesByUser = rows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).ToList());

        return items
            .Select(item => item with { Roles = rolesByUser.GetValueOrDefault(item.Id) ?? [] })
            .ToList();
    }
}
