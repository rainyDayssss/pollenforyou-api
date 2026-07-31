using FluentValidation;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>POST /api/orders/confirm</c> (SRS §2.6).
/// </summary>
public class OrderConfirmationValidator : AbstractValidator<OrderConfirmationRequestDto>
{
    public OrderConfirmationValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .WithMessage("Order number is required.")
            .MaximumLength(30);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one order item is required.");

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

        RuleFor(x => x.Payment)
            .NotNull()
            .WithMessage("Payment details are required.");

        When(x => x.Payment is not null, () =>
        {
            RuleFor(x => x.Payment!.PaymentStage)
                .Must(IsValidPaymentStage)
                .WithMessage("Payment stage must be 'Downpayment', 'Final Balance', or 'Full Payment'.");

            RuleFor(x => x.Payment!.PaymentMethod)
                .Must(IsValidPaymentMethod)
                .WithMessage("Payment method must be 'GCash', 'BDO', 'BPI', or 'Cash'.");

            RuleFor(x => x.Payment!.AmountPaid)
                .GreaterThan(0)
                .WithMessage("Amount paid must be greater than zero.");

            RuleFor(x => x.Payment!.TransactionReference)
                .MaximumLength(100)
                .When(x => x.Payment!.TransactionReference is not null)
                .WithMessage("Transaction reference must be at most 100 characters.");
        });
    }

    private static bool IsValidPaymentStage(string stage)
    {
        return stage is PaymentStages.Downpayment or PaymentStages.FinalBalance or PaymentStages.FullPayment;
    }

    private static bool IsValidPaymentMethod(string method)
    {
        return method is PaymentMethods.GCash or PaymentMethods.BDO or PaymentMethods.BPI or PaymentMethods.Cash;
    }
}
