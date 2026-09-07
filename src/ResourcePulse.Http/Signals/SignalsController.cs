using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.Http.Signals;

// The triage surface (ADR-0032 §13).
//
// A read model of a DOMAIN aggregate — signals ARE an aggregate — so this is
// conformant with ADR-0028 and is not a page-shaped bundle: the dashboard is its
// first consumer, not its definition.
//
// The two write gestures deliberately do NOT ride the plan command envelope.
// POST /api/plan/commands is for MUTATING THE PLAN; accepting a risk moves no
// hours, creates no coverage and touches no demand.
[Route("api/signals")]
public sealed class SignalsController(ISignalService service) : ControllerFoundation
{
    // Live signals, already ranked. Acknowledged ones are INCLUDED: an assumed
    // risk stays visible, distinct from resolved. The queue budget is applied by
    // the caller — it is presentation, and the full count is what lets the page
    // say "7 di 40" honestly.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SignalDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] SignalScope scope = SignalScope.All,
        CancellationToken ct = default) =>
        FromResult(await service.GetAsync(scope, ct));

    // "Cosa è cambiato". `since` defaults to this user's previous visit.
    [HttpGet("changes")]
    [ProducesResponseType<IReadOnlyList<SignalChangeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChanges(
        [FromQuery] SignalScope scope = SignalScope.All,
        [FromQuery] DateTimeOffset? since = null,
        [FromQuery] int max = 4,
        CancellationToken ct = default) =>
        FromResult(await service.GetChangesAsync(scope, since, max, ct));

    // Whether we have looked, and when. The client renders THREE states from
    // this, not two: an empty queue is only "il piano tiene" when LastSweptAt is
    // set and recent.
    [HttpGet("sweep")]
    [ProducesResponseType<SignalSweepDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSweep(CancellationToken ct) =>
        FromResult(await service.GetSweepAsync(ct));

    // Accepting a risk is a planning decision, authored and motivated.
    [RequirePlanner]
    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType<SignalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Acknowledge(
        Guid id, [FromBody] AcknowledgeSignalDto dto, CancellationToken ct) =>
        FromResult(await service.AcknowledgeAsync(id, dto.Reason, ct));

    [RequirePlanner]
    [HttpPost("{id:guid}/reopen")]
    [ProducesResponseType<SignalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken ct) =>
        FromResult(await service.ReopenAsync(id, ct));

    // Deliberately NOT [RequirePlanner], even though it writes. It stores no plan
    // data — it is a personal bookmark — and refusing it to a Viewer would mean
    // the "unseen" dot never works for a Viewer at all. Same reasoning that makes
    // the development role switch Viewer-guarded (ADR-0030 §8): the mechanism has
    // to stay reachable by the people it exists for.
    //
    // Annotated EXPLICITLY rather than left to the Viewer fallback: on a write,
    // "no attribute" and "deliberately Viewer" must not look the same.
    [RequireViewer]
    [HttpPost("visit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordVisit(CancellationToken ct) =>
        FromResult(await service.RecordVisitAsync(ct));
}
