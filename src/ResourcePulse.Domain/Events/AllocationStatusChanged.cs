using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Domain.Events;

// Raised on Allocation.ChangeStatus(...) when the value actually changes.
// Reason is optional and free-form: provenance hook for downgrade cascades
// (ADR-0015 §4) — e.g. "ProjectCommitmentDowngrade" when the cascade demotion
// triggered by a project downgrade flips a Hard block to Tentative.
public sealed record AllocationStatusChanged(
    Guid AllocationId,
    AllocationStatus OldStatus,
    AllocationStatus NewStatus,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
