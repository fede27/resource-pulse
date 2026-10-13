using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Common.Results;
using ResourcePulse.Http;

namespace ResourcePulse.Application.Tests;

// The one place a ServiceError becomes an HTTP response. Every controller goes
// through it, so its body shape IS the API's error contract: a ProblemDetails
// at the top level, with the reason in `detail`. The clients read exactly that
// (useApiError, the commitment-downgrade confirmation) and show a generic
// fallback when it is missing — which is how a refusal loses its reason.
public class ControllerFoundationTests
{
    private sealed class Probe : ControllerFoundation
    {
        public Probe() => ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        public IActionResult Map<T>(ServiceResult<T> result) => FromResult(result);
    }

    [Theory]
    [InlineData(ServiceErrorKind.NotFound, 404)]
    [InlineData(ServiceErrorKind.Conflict, 409)]
    [InlineData(ServiceErrorKind.Forbidden, 403)]
    [InlineData(ServiceErrorKind.Failure, 500)]
    public void AnError_IsAProblemDetailsAtTheTopLevel_CarryingTheReason(ServiceErrorKind kind, int status)
    {
        const string reason = "Allocation cannot be set to Hard: the project root commitment level is 'Planned'.";
        var failure = ServiceResult<int>.Failure(new ServiceError(kind, reason));

        var response = new Probe().Map(failure).Should().BeOfType<ObjectResult>().Subject;

        response.StatusCode.Should().Be(status);
        // The body is the ProblemDetails — not another result wrapped around it.
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be(reason);
        problem.Status.Should().Be(status);
    }

    [Fact]
    public void AValidationError_KeepsItsFieldErrors()
    {
        var failure = ServiceResult<int>.Failure(ServiceError.Validation(
            new Dictionary<string, string[]> { ["percent"] = ["Must be positive."] }));

        var response = new Probe().Map(failure).Should().BeOfType<BadRequestObjectResult>().Subject;

        response.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey("percent");
    }

    [Fact]
    public void ASuccess_IsTheValue()
    {
        new Probe().Map(ServiceResult<int>.Success(42))
            .Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(42);
    }
}
