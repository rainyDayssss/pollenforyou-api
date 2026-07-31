using FluentValidation;
using PollenForYouApi.DTOs;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>PATCH /api/admin/products/{id}</c> (SRS §2.6).
/// Optional PATCH semantics: supplied fields are validated, and at least one
/// field must be present.
/// </summary>
public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequestDto>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x)
            .Must(HaveAtLeastOneField)
            .WithMessage("At least one field must be provided for update.")
            .WithName(nameof(UpdateProductRequestDto));

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .When(x => x.CategoryId.HasValue)
            .WithMessage("Category must be a valid id.");

        RuleFor(x => x.ProductCode)
            .NotEmpty()
            .When(x => x.ProductCode is not null)
            .WithMessage("Product code cannot be empty.")
            .MaximumLength(20)
            .When(x => x.ProductCode is not null)
            .WithMessage("Product code must be at most 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .When(x => x.Name is not null)
            .WithMessage("Name cannot be empty.")
            .MaximumLength(150)
            .When(x => x.Name is not null)
            .WithMessage("Name must be at most 150 characters.");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.BasePrice.HasValue)
            .WithMessage("Base price must be zero or greater.");
    }

    private static bool HaveAtLeastOneField(UpdateProductRequestDto dto)
    {
        return dto.CategoryId.HasValue
               || dto.ProductCode is not null
               || dto.Name is not null
               || dto.BasePrice.HasValue
               || dto.IsActive.HasValue;
    }
}
