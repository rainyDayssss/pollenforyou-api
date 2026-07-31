namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown when an operation collides with the current state of a resource — e.g. a
/// workspace claim lost to another admin, a settlement on an unclaimable order, or
/// a concurrent status change. Mapped to <c>409 Conflict</c> by the global
/// exception handler.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
