using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Services.Signals;

// What one detector pass did. Returned so the worker can log it and the
// application tests can assert on reconciliation rather than on the database.
public sealed record SignalSweepResult
{
    public DateOnly Today { get; init; }
    public DateOnly HorizonEnd { get; init; }

    public int Detected { get; init; }
    public int Created { get; init; }
    public int Resolved { get; init; }
    public int Worsened { get; init; }
    public int Purged { get; init; }

    // Live rows left behind — what the dashboard will read, and what
    // SignalSweepState records so an empty queue can be told apart from a sweep
    // that never ran (ADR-0032 §10).
    public int LiveCount { get; init; }
}

// One condition as measured by the detector, before reconciliation against what
// is already on file. Kind + SubjectId is the natural key (ADR-0032 §2).
public sealed record DetectedSignal(SignalKind Kind, Guid? SubjectId, SignalObservation Observation);
