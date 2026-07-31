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

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null)
            .WithMessage("Description must be at most 1000 characters.");

        RuleFor(x => x.ImageUrl)
            .Must(BeValidOrEmpty)
            .When(x => x.ImageUrl is not null)
            .WithMessage("Image URL must be a valid http(s) URL.")
            .MaximumLength(2048)
            .When(x => x.ImageUrl is not null)
            .WithMessage("Image URL must be at most 2048 characters.");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Base price must be zero or greater.");
    }

    private static bool BeValidOrEmpty(string? value)
    {
        // Empty string is allowed so a PATCH can clear the field (consistent
        // with Description); non-empty values must be absolute http(s) URLs.
        return string.IsNullOrEmpty(value) || IsValidHttpUrl(value);
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
