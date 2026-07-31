namespace PollenForYouApi.DTOs;

/// <summary>
/// Inbound contract for registering a new Admin or Superadmin account.
/// </summary>
public record CreateUserRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}
