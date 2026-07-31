namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown when a requested resource (e.g., a user account) cannot be found.
/// Mapped to <c>404 Not Found</c> by the controller.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
