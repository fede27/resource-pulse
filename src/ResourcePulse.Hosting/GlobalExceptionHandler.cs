using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ResourcePulse.Hosting;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        logger.LogError(exception,
            "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

        var detail = environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred.";

        var problem = new ProblemDetails
        {
            Status = 500,
            Title = "Internal Server Error",
            Detail = detail,
            Extensions = { ["correlationId"] = correlationId }
        };

        httpContext.Response.StatusCode = 500;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
