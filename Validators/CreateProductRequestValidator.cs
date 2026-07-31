using FluentValidation;
using PollenForYouApi.DTOs;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>POST /api/admin/products</c> (SRS §2.6).
/// </summary>
public class CreateProductRequestValidator : AbstractValidator<CreateProductRequestDto>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage("Category is required.");

        RuleFor(x => x.ProductCode)
            .NotEmpty()
            .WithMessage("Product code is required.")
            .MaximumLength(20)
            .WithMessage("Product code must be at most 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(150)
            .WithMessage("Name must be at most 150 characters.");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Base price must be zero or greater.");
    }
}
