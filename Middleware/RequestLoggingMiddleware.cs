using System.Diagnostics;

namespace PollenForYouApi.Middleware;

/// <summary>
/// Logs every HTTP request: method, path, response status and elapsed duration.
/// Registered outermost in the pipeline so it observes the final status code even
/// when <see cref="GlobalExceptionHandler"/> renders a ProblemDetails response.
/// 5xx → Error, 4xx → Warning, everything else → Information.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error
                : statusCode >= 400 ? LogLevel.Warning
                : LogLevel.Information;

            _logger.Log(level,
                "{Method} {Path} → {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
