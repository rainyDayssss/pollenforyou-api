namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown when authentication fails — bad credentials, or an invalid, expired, or
/// revoked refresh token. Mapped to <c>401 Unauthorized</c> by the auth controller.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
