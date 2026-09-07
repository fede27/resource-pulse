using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Signals;

// The detector (ADR-0032 §9). The ONLY writer of the detection axis: nothing
// else may set a signal Live or Resolved.
//
// Runs on a schedule, in a dedicated worker, because signals change WITHOUT
// anybody mutating anything: the fence rolls from today, so a tentative crosses
// into the frozen zone while everyone sleeps and a demand enters the committing
// horizon simply because a day passed. That is why detection cannot ride domain
// events — and why ADR-0004 does not need reversing.
public interface ISignalDetectionService
{
    // One full pass over the current tenant: detect, reconcile against the live
    // set, resolve what is no longer observed, purge beyond the retention window,
    // and record the sweep. One SaveChanges.
    //
    // `today` is a parameter rather than DateTime.UtcNow so the pass is
    // deterministic and testable — the fence, the deadlines and the admission
    // rule are all relative to it.
    Task<ServiceResult<SignalSweepResult>> SweepAsync(DateOnly today, CancellationToken ct = default);

    // The latency half of the pair (ADR-0032 §9): after a committed plan mutation,
    // resolve the touched signals whose condition no longer holds.
    //
    // It may RESOLVE, never CREATE — an asymmetry with two independent reasons.
    // It kills flapping at the source (the daily sweep damps it by observing once
    // a day; immediate re-evaluation would not). And it says the right thing: the
    // dashboard tells you about what you did NOT just do. Create a demand and you
    // do not need a queue announcing it two seconds later; cover a gap and you
    // want the row gone at once.
    //
    // The timer remains the correctness guarantee: calendars, closures and
    // capacity all change through controllers that are not the envelope, and they
    // move hours and therefore gaps.
    Task<ServiceResult<int>> ResolveStaleAsync(
        SignalTouch touch, DateOnly today, CancellationToken ct = default);
}

// What a committed plan mutation touched, as natural keys. Only signals whose
// (kind, subject) appears here are candidates for the resolve-only pass.
public sealed record SignalTouch(
    IReadOnlyCollection<Guid> DemandIds,
    IReadOnlyCollection<Guid> AllocationIds,
    IReadOnlyCollection<Guid> ResourceIds)
{
    public static readonly SignalTouch Empty = new([], [], []);

    public bool IsEmpty => DemandIds.Count == 0 && AllocationIds.Count == 0 && ResourceIds.Count == 0;
}
