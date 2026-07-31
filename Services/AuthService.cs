using Microsoft.AspNetCore.Identity;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;
using PollenForYouApi.Repositories;

namespace PollenForYouApi.Services;

/// <summary>
/// Authentication orchestration (SRS §3.1.4 / §4): verifies credentials, issues a
/// JWT access token + hashed refresh-token pair, rotates the pair on renewal, and
/// revokes sessions on logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct)
    {
        // FindByEmailAsync inherits the IsActive global query filter (AGENT.md §12),
        // so soft-deleted admins cannot authenticate.
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Deliberately identical message for unknown email vs wrong password
            // to avoid account enumeration.
            _logger.LogWarning("Failed login attempt for email {Email}", dto.Email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        return await IssueTokenPairAsync(user, ct);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshRequestDto dto, CancellationToken ct)
    {
        var stored = await _refreshTokenRepository.GetByHashAsync(_tokenService.HashToken(dto.RefreshToken), ct);

        if (stored is null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (stored.IsRevoked)
        {
            // Rotation-reuse detection: replaying a consumed token is a credential
            // compromise signal — kill the entire token family for that user.
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoked all refresh sessions", stored.UserId);
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, ct);
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            // Account was soft-deleted after the session was issued (the IsActive
            // query filter hides it) — invalidate the session.
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, ct);
            throw new UnauthorizedException("Account is no longer available.");
        }

        // Rotation: consume the presented token and issue a fresh pair. If a
        // concurrent request already consumed it (conditional revoke returned
        // false), treat it as reuse and kill the token family.
        var revoked = await _refreshTokenRepository.RevokeAsync(stored, ct);
        if (!revoked)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, ct);
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        return await IssueTokenPairAsync(user, ct);
    }

    public async Task LogoutAsync(int userId, CancellationToken ct)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, ct);
        _logger.LogInformation("User {UserId} logged out; all refresh sessions revoked", userId);
    }

    private async Task<AuthResponseDto> IssueTokenPairAsync(ApplicationUser user, CancellationToken ct)
    {
        var (accessToken, expiresInSeconds, roles) = await _tokenService.CreateAccessTokenAsync(user, ct);
        var refreshToken = await _tokenService.CreateRefreshTokenSessionAsync(user.Id, ct);

        _logger.LogInformation("User {Email} authenticated with roles {Roles}", user.Email, roles);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = expiresInSeconds,
            Email = user.Email ?? string.Empty,
            Roles = roles
        };
    }
}
