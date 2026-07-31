using FluentValidation;
using PollenForYouApi.DTOs;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>POST /api/public/checkout/submit</c> (SRS §2.6).
/// Messenger username case is preserved verbatim — never normalized here.
/// </summary>
public class CheckoutRequestDtoValidator : AbstractValidator<CheckoutRequestDto>
{
    public CheckoutRequestDtoValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .WithMessage("Customer name is required.")
            .MaximumLength(150)
            .WithMessage("Customer name must be at most 150 characters.");

        RuleFor(x => x.CustomerMessengerUsername)
            .NotEmpty()
            .WithMessage("Messenger username is required.")
            .MaximumLength(100)
            .WithMessage("Messenger username must be at most 100 characters.");

        RuleFor(x => x.ReceiverName)
            .NotEmpty()
            .WithMessage("Receiver name is required.")
            .MaximumLength(150)
            .WithMessage("Receiver name must be at most 150 characters.");

        RuleFor(x => x.ReceiverContactNumber)
            .NotEmpty()
            .WithMessage("Receiver contact number is required.")
            .MaximumLength(20)
            .WithMessage("Receiver contact number must be at most 20 characters.");

        RuleFor(x => x.DeliveryAddress)
            .NotEmpty()
            .WithMessage("Delivery address is required.")
            .MaximumLength(500)
            .WithMessage("Delivery address must be at most 500 characters.");

        RuleFor(x => x.DeliveryDate)
            .Must(d => d != default)
            .WithMessage("Delivery date is required.");

        RuleFor(x => x.MessageOnCard)
            .MaximumLength(500)
            .When(x => x.MessageOnCard is not null)
            .WithMessage("Message on card must be at most 500 characters.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .GreaterThan(0)
                    .WithMessage("A valid product is required.");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be at least 1.");
            });
    }
}
