using FluentValidation;
using PollenForYouApi.DTOs;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>POST /api/admin/categories</c> (SRS §2.6).
/// </summary>
public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequestDto>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Category name is required.")
            .MaximumLength(100)
            .WithMessage("Category name must be at most 100 characters.");
    }
}
