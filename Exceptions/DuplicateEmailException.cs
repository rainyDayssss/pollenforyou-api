namespace PollenForYouApi.Exceptions;

/// <summary>
/// Thrown by the user service when an email already exists — either detected by the
/// app-layer Identity validator or by the DB-level unique filtered index on
/// <c>NormalizedEmail</c> (which also catches collisions with soft-deleted accounts).
/// Mapped to <c>409 Conflict</c> by the controller.
/// </summary>
public class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("A user with this email address already exists.")
    {
    }
}
