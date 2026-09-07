using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Plan;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.Http.Plan;

// Single plan-mutation endpoint (ADR-0018). The body is a typed command — a
// discriminated union with the "kind" discriminator. One mechanism, many
// intents. dryRun on the command returns the computed consequence without
// committing. New gestures arrive as new command kinds, not new endpoints.
//
// The per-command FluentValidation validators are resolved by the
// DtoValidationFilter on the runtime type of the bound body.
[Route("api/plan")]
[ApiController]
public sealed class PlanCommandsController(
    IPlanCommandService service,
    ISignalDetectionService signals,
    TimeProvider clock) : ControllerFoundation
{
    [RequirePlanner]
    [HttpPost("commands")]
    [ProducesResponseType<PlanCommandResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecuteAsync([FromBody] PlanCommand command, CancellationToken ct)
    {
        var result = await service.ExecuteAsync(command, ct);

        // The latency half of ADR-0032 §9, wired at the envelope's single call
        // site rather than inside PlanCommandService: the plan writer stays
        // unaware that triage exists, and a unit test of it needs no detector.
        //
        // RESOLVE ONLY, and never on a dry run. Covering a gap should make the
        // row disappear at once; creating one needs no announcement two seconds
        // after you created it. The scheduled sweep remains the correctness
        // guarantee — capacity, calendars and closures move gaps through
        // controllers that are not this one.
        if (result is { IsSuccess: true, Value.Committed: true })
            await signals.ResolveStaleAsync(TouchOf(result.Value), Today(), ct);

        return FromResult(result);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    // The natural keys the command touched. Deleted coverage still counts: its
    // TentativeInFrozen signal is exactly the one that must go.
    private static SignalTouch TouchOf(PlanCommandResult result) => new(
        DemandIds: result.Changes.Select(c => c.DemandId)
            .Concat(result.DemandChanges.Select(d => d.Id))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList(),
        AllocationIds: result.Changes.Select(c => c.Id)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList(),
        ResourceIds: result.Changes.Select(c => c.ResourceId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList());
}
