using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PollenForYouApi.Entities;
using PollenForYouApi.Options;
using PollenForYouApi.Repositories;

namespace PollenForYouApi.Services;

/// <summary>
/// JWT access-token generation with Identity role claims, and opaque refresh-token
/// sessions stored as SHA-256 hashes.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public TokenService(
        IOptions<JwtOptions> jwtOptions,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _jwt = jwtOptions.Value;
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<(string Token, int ExpiresInSeconds, IReadOnlyList<string> Roles)> CreateAccessTokenAsync(
        ApplicationUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_jwt.AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, _jwt.AccessTokenLifetimeMinutes * 60, roles.ToList());
    }

    public async Task<string> CreateRefreshTokenSessionAsync(int userId, CancellationToken ct)
    {
        var rawToken = GenerateRefreshToken();

        var entity = new UserRefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenLifetimeDays),
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(entity, ct);

        return rawToken;
    }

    public string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string GenerateRefreshToken()
    {
        // 64 cryptographically-random bytes, base64url-encoded (URL-safe, no padding).
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
    }
}
