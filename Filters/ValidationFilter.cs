using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using PollenForYouApi.Middleware;

namespace PollenForYouApi.Filters;

/// <summary>
/// FluentValidation pipeline filter (SRS §2.6): validates inbound request DTOs
/// before they reach application services. Failures throw a FluentValidation
/// <see cref="ValidationException"/> which the centralized
/// <see cref="GlobalExceptionHandler"/> renders as a uniform <c>400 Bad Request</c>
/// validation problem response — a single error-formatting path for every request.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(new ValidationContext<object>(argument));

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next();
    }
}
