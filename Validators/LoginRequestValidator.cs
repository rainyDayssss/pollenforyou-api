using FluentValidation;
using PollenForYouApi.DTOs;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>POST /api/auth/login</c> (SRS §2.6).
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .MaximumLength(256)
            .EmailAddress()
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
