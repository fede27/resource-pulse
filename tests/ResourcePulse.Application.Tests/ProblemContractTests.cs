using System.Text.Json;
using System.Text.RegularExpressions;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Http;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Plan;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Application.Tests;

// The refusal contract (ADR-0037), checked where it matters: in the BYTES a
// client receives. ControllerFoundationTests looks at the result object; the
// 2026-10-13 bug was invisible there — the object was right and its
// serialization was not — so these tests run the result through MVC's own
// executor and read the JSON back.
public class ProblemContractTests
{
    private static readonly IServiceProvider Mvc = new ServiceCollection()
        .AddLogging()
        .AddControllers().Services
        .BuildServiceProvider();

    private sealed class Probe : ControllerFoundation
    {
        public Probe(HttpContext http) => ControllerContext = new ControllerContext { HttpContext = http };

        public IActionResult Map<T>(ServiceResult<T> result) => FromResult(result);
    }

    private static async Task<(int Status, JsonElement Body)> OnTheWireAsync(ServiceError error)
    {
        var http = new DefaultHttpContext { RequestServices = Mvc };
        http.Response.Body = new MemoryStream();

        var result = new Probe(http).Map(ServiceResult<int>.Failure(error));
        await result.ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));

        http.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(http.Response.Body);
        return (http.Response.StatusCode, document.RootElement.Clone());
    }

    [Theory]
    [InlineData(ServiceErrorKind.NotFound, 404)]
    [InlineData(ServiceErrorKind.Conflict, 409)]
    [InlineData(ServiceErrorKind.Forbidden, 403)]
    [InlineData(ServiceErrorKind.Failure, 500)]
    public async Task AnUncodedRefusal_IsAProblemDetailsAtTheTopLevel(ServiceErrorKind kind, int status)
    {
        var (code, body) = await OnTheWireAsync(new ServiceError(kind, "The reason."));

        code.Should().Be(status);
        body.GetProperty("status").GetInt32().Should().Be(status);
        body.GetProperty("detail").GetString().Should().Be("The reason.");
        // Nothing wrapped around it: the shape the bug produced had these.
        body.TryGetProperty("value", out _).Should().BeFalse();
        body.TryGetProperty("formatters", out _).Should().BeFalse();
        // No code, no args: the client falls back to `detail`.
        body.TryGetProperty("args", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ACodedRefusal_CarriesItsTypeAndItsPiecesInWireFormat()
    {
        var error = ServiceError.Conflict(
            ProblemCode.HardRequiresCommitment,
            "Allocation cannot be set to Hard.",
            new Dictionary<string, object?>
            {
                ["level"] = CommitmentLevel.Planned,
                ["required"] = new[] { CommitmentLevel.Committed, CommitmentLevel.Critical },
            });

        var (status, body) = await OnTheWireAsync(error);

        status.Should().Be(409);
        body.GetProperty("type").GetString().Should().Be("urn:resourcepulse:problem:hard-requires-commitment");
        body.GetProperty("detail").GetString().Should().Be("Allocation cannot be set to Hard.");
        var args = body.GetProperty("args");
        // Enums travel as integers, exactly as every DTO carries them.
        args.GetProperty("level").GetInt32().Should().Be((int)CommitmentLevel.Planned);
        args.GetProperty("required").EnumerateArray().Select(e => e.GetInt32())
            .Should().Equal((int)CommitmentLevel.Committed, (int)CommitmentLevel.Critical);
    }

    [Fact]
    public async Task ACodeWithoutPieces_StillSendsAnArgsObject()
    {
        var (_, body) = await OnTheWireAsync(new ServiceError(
            ServiceErrorKind.Conflict, "x", Code: ProblemCode.HardRequiresCommitment));

        body.GetProperty("args").ValueKind.Should().Be(JsonValueKind.Object);
    }

    // ── The catalogue ────────────────────────────────────────────────────────

    [Fact]
    public void EverySlug_IsKebabCase_AndUnique()
    {
        var slugs = Enum.GetValues<ProblemCode>().Select(ProblemCodes.Slug).ToList();

        slugs.Should().OnlyContain(s => Regex.IsMatch(s, "^[a-z0-9]+(-[a-z0-9]+)*$"));
        slugs.Should().OnlyHaveUniqueItems();
        ProblemCodes.Slug(ProblemCode.CommitmentDowngradeDemotesHard)
            .Should().Be("commitment-downgrade-demotes-hard");
    }

    [Fact]
    public void ACaughtDomainException_KeepsItsCode()
    {
        var args = new Dictionary<string, object?> { ["count"] = 2 };
        var caught = new DomainException(ProblemCode.CommitmentDowngradeDemotesHard, "message", args);

        var error = ServiceResult<int>.Conflict(caught).Error!;

        error.Kind.Should().Be(ServiceErrorKind.Conflict);
        error.Code.Should().Be(ProblemCode.CommitmentDowngradeDemotesHard);
        error.Args.Should().BeSameAs(args);
        error.Message.Should().Be("message");
    }

    // ── The two refusals coded first ─────────────────────────────────────────

    [Fact]
    public async Task HardOnAPlannedRoot_IsCoded_WithTheLevelAndTheRequiredOnes()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Planned);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodStart = new DateOnly(2026, 6, 1),
            PeriodEnd = new DateOnly(2026, 6, 14),
            Percent = 40m,
            Status = AllocationStatus.Hard,
        });

        var error = result.Error!;
        error.Code.Should().Be(ProblemCode.HardRequiresCommitment);
        error.Args!["level"].Should().Be(CommitmentLevel.Planned);
        error.Args["required"].Should().BeEquivalentTo(new[] { CommitmentLevel.Committed, CommitmentLevel.Critical });
    }

    [Fact]
    public async Task ADowngradeThatWouldDemoteHard_IsCoded_WithTheCount_AndConfirmingGoesThrough()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Committed);
        foreach (var start in new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1) })
        {
            (await h.Service.ExecuteAsync(new CreateCommand
            {
                DemandId = h.DemandId,
                ResourceId = h.ResourceId,
                PeriodStart = start,
                PeriodEnd = start.AddDays(13),
                Percent = 40m,
                Status = AllocationStatus.Hard,
            })).IsSuccess.Should().BeTrue();
        }
        h.Db.ChangeTracker.Clear();

        var mapping = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(mapping);
        var projects = new ProjectNodeService(
            new Repository<ProjectNode, Guid>(h.Db), h.Db, new Mapper(mapping), h.CommitmentPolicy);

        UpdateProjectDto Downgrade(bool confirm) => new()
        {
            Type = ProjectType.Transform,
            CommitmentLevel = CommitmentLevel.Planned,
            ConfirmDemoteHardAllocations = confirm,
        };

        var refused = await projects.UpdateProjectAsync(h.ProjectNodeId, Downgrade(false));

        var error = refused.Error!;
        error.Kind.Should().Be(ServiceErrorKind.Conflict);
        error.Code.Should().Be(ProblemCode.CommitmentDowngradeDemotesHard);
        error.Args!["count"].Should().Be(2);
        error.Args["from"].Should().Be(CommitmentLevel.Committed);
        error.Args["to"].Should().Be(CommitmentLevel.Planned);

        h.Db.ChangeTracker.Clear();
        (await projects.UpdateProjectAsync(h.ProjectNodeId, Downgrade(true))).IsSuccess.Should().BeTrue();
    }
}
