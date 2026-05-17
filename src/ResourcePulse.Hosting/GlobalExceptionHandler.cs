using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Common.Domain;

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

        if (exception is DomainException domainEx)
        {
            logger.LogWarning(domainEx,
                "Domain rule violation. CorrelationId: {CorrelationId}", correlationId);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Domain rule violation",
                Detail = domainEx.Message,
                Extensions = { ["correlationId"] = correlationId }
            };

            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        logger.LogError(exception,
            "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

        var detail = environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred.";

        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = detail,
            Extensions = { ["correlationId"] = correlationId }
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}
