using MachineMonitoring.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MachineMonitoring.Api.Errors;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        (int statusCode, string title, string detail) = GetProblemDetails(exception);

        LogException(exception, statusCode);

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Detail) GetProblemDetails(
        Exception exception
    )
    {
        return exception switch
        {
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                exception.Message
            ),

            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                exception.Message
            ),

            InvalidOperationException => (
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation",
                exception.Message
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred."
            ),
        };
    }

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "An unexpected error occurred while processing the request."
            );

            return;
        }

        _logger.LogWarning(exception, "Request failed with status code {StatusCode}.", statusCode);
    }
}
