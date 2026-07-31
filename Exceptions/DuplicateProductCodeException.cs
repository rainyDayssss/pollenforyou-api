namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown when a catalog item's <c>ProductCode</c> collides with an existing row —
/// detected either by a repository pre-check or by the DB-level unique index on
/// <c>ProductCode</c> (which also catches soft-deleted products still holding the
/// code). Mapped to <c>409 Conflict</c> by the global exception handler.
/// </summary>
public class DuplicateProductCodeException : Exception
{
    public DuplicateProductCodeException()
        : base("A product with this code already exists.")
    {
    }
}
