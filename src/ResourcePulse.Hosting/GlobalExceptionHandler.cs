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

        // 409, exactly as ControllerFoundation renders a ServiceResult.Conflict —
        // which is what a service produces when it CATCHES this same exception (the
        // documented convention). Two status codes for one failure, chosen by
        // whether somebody remembered a try/catch, is not a distinction a client can
        // act on: it would have to handle both everywhere, and any service that
        // later grows a catch would silently change its contract.
        //
        // The "a rule was violated" nuance survives where it is useful — in the
        // warning below, next to the correlation id.
        if (exception is DomainException domainEx)
        {
            logger.LogWarning(domainEx,
                "Domain rule violation reached the handler uncaught. CorrelationId: {CorrelationId}",
                correlationId);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = domainEx.Message,
                Extensions = { ["correlationId"] = correlationId }
            };

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
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
