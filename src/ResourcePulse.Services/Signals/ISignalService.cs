using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Signals;

// The triage read model plus the two human gestures (ADR-0032 §13).
//
// Deliberately NOT part of the plan command envelope: POST /api/plan/commands is
// the surface for MUTATING THE PLAN, and accepting a risk moves no hours, creates
// no coverage and touches no demand. Routing it through the envelope would make
// "one gesture = one kind" stop meaning "one gesture on the plan".
public interface ISignalService
{
    // Live signals, already ranked (ADR-0032 §11). Acknowledged ones are INCLUDED:
    // an assumed risk stays visible, distinct from resolved. The queue budget is
    // applied by the caller — it is presentation, and the total is what lets the
    // page say "7 di 40".
    Task<ServiceResult<IReadOnlyList<SignalDto>>> GetAsync(
        SignalScope scope, CancellationToken ct = default);

    // The change feed. `since` defaults to this user's previous visit.
    Task<ServiceResult<IReadOnlyList<SignalChangeDto>>> GetChangesAsync(
        SignalScope scope, DateTimeOffset? since, int max, CancellationToken ct = default);

    Task<ServiceResult<SignalSweepDto>> GetSweepAsync(CancellationToken ct = default);

    Task<ServiceResult<SignalDto>> AcknowledgeAsync(
        Guid id, string? reason, CancellationToken ct = default);

    Task<ServiceResult<SignalDto>> ReopenAsync(Guid id, CancellationToken ct = default);

    // Moves this user's "seen" marker forward. A write a Viewer may perform: it
    // stores no plan data, and refusing it would mean the unseen dot never works
    // for a Viewer (same reasoning as the dev role switch being Viewer-guarded).
    Task<ServiceResult<Unit>> RecordVisitAsync(CancellationToken ct = default);
}
