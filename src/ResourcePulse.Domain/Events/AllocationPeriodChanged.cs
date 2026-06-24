namespace ResourcePulse.Domain.Events;

// Raised on Allocation.ChangePeriod(...) when the window actually changes.
// Reason is optional decision-level provenance (ADR-0017), mirroring
// AllocationStatusChanged (ADR-0015): "Shift", "Resize", "ShiftFrom" for the
// span operations; null from the pre-existing Update / Move callers.
public sealed record AllocationPeriodChanged(
    Guid AllocationId,
    DateOnly OldStart,
    DateOnly OldEnd,
    DateOnly NewStart,
    DateOnly NewEnd,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
