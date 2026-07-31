using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PollenForYouApi.Exceptions;

namespace PollenForYouApi.Middleware;

/// <summary>
/// Centralized exception handler (SRS §4): every unhandled exception flows through
/// here and is rendered as a uniform RFC 7807 <c>ProblemDetails</c> response.
/// Controllers must never hand-craft error responses — they throw and let this
/// handler format the body.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, errors) = exception switch
        {
            NotFoundException ex => (
                StatusCodes.Status404NotFound, "Not Found", ex.Message, (Dictionary<string, string[]>?)null),
            UnauthorizedException ex => (
                StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message, null),
            DuplicateEmailException ex => (
                StatusCodes.Status409Conflict, "Conflict", ex.Message, null),
            DuplicateProductCodeException ex => (
                StatusCodes.Status409Conflict, "Conflict", ex.Message, null),
            ConflictException ex => (
                StatusCodes.Status409Conflict, "Conflict", ex.Message, null),
            ValidationException ex => (
                StatusCodes.Status400BadRequest, "Validation Failed", "Validation failed.", ToErrors(ex)),
            _ => (
                StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.", null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            // Known client errors (400/401/404/409) are expected traffic — log at
            // Information to avoid flooding; reserve Error for the 500 path above.
            _logger.LogInformation(exception,
                "Request failed with {StatusCode} on {Method} {Path}",
                statusCode, httpContext.Request.Method, httpContext.Request.Path);
        }

        ProblemDetails problemDetails = errors is not null
            ? new ValidationProblemDetails(errors)
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
            : new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            };

        await Results.Problem(problemDetails).ExecuteAsync(httpContext);
        return true;
    }

    /// <summary>
    /// Groups FluentValidation failures by property name into the field-level
    /// <c>errors</c> dictionary of the 400 response (same shape as the
    /// <see cref="Filters.ValidationFilter"/> output).
    /// </summary>
    private static Dictionary<string, string[]> ToErrors(ValidationException ex)
    {
        return ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
