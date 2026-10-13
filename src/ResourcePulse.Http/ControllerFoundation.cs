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

    // `Problem(...)` already IS the response: an ObjectResult carrying the
    // ProblemDetails and its status code. It must be returned as it is. Wrapping
    // it in NotFound(...) / Conflict(...) serializes the ObjectResult ITSELF as
    // the body — {"value":{...},"formatters":[],...} — which keeps the status
    // code and buries `detail` one level down, where no client looks: every
    // refusal then reads "an error occurred" and the reason is lost. That is
    // what this method did from the first controller until 2026-10-13.
    private IActionResult ErrorResponse(ServiceError error) =>
        error.Kind switch
        {
            ServiceErrorKind.NotFound   => Problem(error.Message, statusCode: 404, title: "Not Found"),
            ServiceErrorKind.Validation => BadRequest(CreateValidationProblem(error)),
            ServiceErrorKind.Conflict   => Problem(error.Message, statusCode: 409, title: "Conflict"),
            ServiceErrorKind.Forbidden  => Problem(error.Message, statusCode: 403, title: "Forbidden"),
            ServiceErrorKind.Failure    => Problem(error.Message, statusCode: 500, title: "Internal Server Error"),
            _                           => Problem(error.Message, statusCode: 500, title: "Internal Server Error")
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
