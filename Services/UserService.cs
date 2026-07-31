using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;
using PollenForYouApi.Repositories;

namespace PollenForYouApi.Services;

/// <summary>
/// Account lifecycle service. Duplicate emails are detected at two levels:
/// the app-layer Identity validator (active accounts) and the DB-level unique
/// filtered index on <c>NormalizedEmail</c> (also catches soft-deleted accounts),
/// both surfaced as <see cref="DuplicateEmailException"/> → <c>409 Conflict</c>.
/// </summary>
public class UserService : IUserService
{
    private const int MaxPageSize = 50;

    private readonly IUserRepository _userRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;

    public UserService(IUserRepository userRepository, UserManager<ApplicationUser> userManager, IMapper mapper)
    {
        _userRepository = userRepository;
        _userManager = userManager;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserResponseDto>> GetUsersAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        return await _userRepository.GetUsersPageAsync(page, pageSize, ct);
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserRequestDto dto, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            IsActive = true
        };

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, dto.Password);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateEmailException();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "DuplicateEmail"))
            {
                throw new DuplicateEmailException();
            }

            throw new ValidationException(
                result.Errors.Select(ToValidationFailure));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, dto.Role);
        if (!roleResult.Succeeded)
        {
            throw new ValidationException(
                roleResult.Errors.Select(ToValidationFailure));
        }

        // The fresh entity's Roles navigation is not loaded; surface the assigned role
        // explicitly so the create response is accurate.
        return _mapper.Map<UserResponseDto>(user) with { Roles = [dto.Role] };
    }

    public async Task<UserResponseDto> ReactivateUserAsync(int id, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdIncludingDeletedAsync(id, ct)
            ?? throw new NotFoundException($"User with id {id} was not found.");

        if (!user.IsActive)
        {
            var updated = await _userRepository.SetIsActiveAsync(id, true, ct);
            if (!updated)
            {
                throw new NotFoundException($"User with id {id} was not found.");
            }
        }

        user.IsActive = true;

        var reactivated = _mapper.Map<UserResponseDto>(user);
        return reactivated with { Roles = await _userRepository.GetRolesForUserAsync(id, ct) };
    }

    public async Task DeleteUserAsync(int id, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdIncludingDeletedAsync(id, ct)
            ?? throw new NotFoundException($"User with id {id} was not found.");

        if (!user.IsActive)
        {
            return;
        }

        var updated = await _userRepository.SetIsActiveAsync(id, false, ct);
        if (!updated)
        {
            throw new NotFoundException($"User with id {id} was not found.");
        }
    }

    /// <summary>
    /// Maps an Identity error onto a FluentValidation failure using the request DTO's
    /// property name so the 400 response groups errors under meaningful keys.
    /// </summary>
    private static ValidationFailure ToValidationFailure(IdentityError error)
    {
        var propertyName = error.Code switch
        {
            "DuplicateEmail" or "InvalidEmail" => nameof(CreateUserRequestDto.Email),
            _ => nameof(CreateUserRequestDto.Password)
        };

        return new ValidationFailure(propertyName, error.Description);
    }

    /// <summary>
    /// Matches SQL Server unique index (2601) / unique constraint (2627) violations
    /// raised by the filtered unique index on <c>NormalizedEmail</c>.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
