namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown when a category name collides with an existing row (case-insensitive).
/// Mapped to <c>409 Conflict</c> by the global exception handler.
/// </summary>
public class DuplicateCategoryNameException : Exception
{
    public DuplicateCategoryNameException()
        : base("A category with this name already exists.")
    {
    }
}
