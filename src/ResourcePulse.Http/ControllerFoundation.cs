using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Http;

[ApiController]
public abstract class ControllerFoundation : ControllerBase
{
    protected IActionResult FromResult<T>(ServiceResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ErrorResponse(result.Error!);

    protected IActionResult FromResult(ServiceResult<Unit> result) =>
        result.IsSuccess ? NoContent() : ErrorResponse(result.Error!);

    // 201 Created with Location = "{currentRequestPath}/{id}".
    // Avoids CreatedAtAction's action-name reverse lookup, which is broken by
    // MvcOptions.SuppressAsyncSuffixInActionNames (default true) — the runtime
    // action name has "Async" stripped, so nameof(GetByIdAsync) never matches.
    protected IActionResult FromCreateResult<T>(ServiceResult<T> result, Func<T, object> idSelector) =>
        result.IsSuccess
            ? Created($"{Request.Path}/{idSelector(result.Value)}", result.Value)
            : ErrorResponse(result.Error!);

    private IActionResult ErrorResponse(ServiceError error) =>
        error.Kind switch
        {
            ServiceErrorKind.NotFound   => NotFound(Problem(error.Message, statusCode: 404, title: "Not Found")),
            ServiceErrorKind.Validation => BadRequest(CreateValidationProblem(error)),
            ServiceErrorKind.Conflict   => Conflict(Problem(error.Message, statusCode: 409, title: "Conflict")),
            ServiceErrorKind.Forbidden  => StatusCode(403, Problem(error.Message, statusCode: 403, title: "Forbidden")),
            ServiceErrorKind.Failure    => StatusCode(500, Problem(error.Message, statusCode: 500, title: "Internal Server Error")),
            _                           => StatusCode(500, Problem(error.Message, statusCode: 500, title: "Internal Server Error"))
        };

    private static ValidationProblemDetails CreateValidationProblem(ServiceError error)
    {
        var details = new ValidationProblemDetails { Status = 400, Detail = error.Message };
        if (error.Details is not null)
            foreach (var (key, values) in error.Details)
                details.Errors[key] = values;
        return details;
    }
}
