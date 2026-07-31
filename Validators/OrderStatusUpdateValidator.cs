using FluentValidation;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Validators;

/// <summary>
/// Boundary validation for <c>PATCH /api/admin/orders/{id}/status</c> (SRS §2.6).
/// Only fulfillment targets are accepted here — <c>In Production</c> is set by
/// settlement, and forward-transition legality is enforced in the service.
/// </summary>
public class OrderStatusUpdateValidator : AbstractValidator<OrderStatusUpdateRequestDto>
{
    public OrderStatusUpdateValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.")
            .Must(IsTargetStatus)
            .WithMessage("Status must be 'Ready for Dispatch', 'Dispatched', 'Completed', or 'Cancelled'.");
    }

    private static bool IsTargetStatus(string status)
    {
        return status is OrderStatuses.ReadyForDispatch
            or OrderStatuses.Dispatched
            or OrderStatuses.Completed
            or OrderStatuses.Cancelled;
    }
}
