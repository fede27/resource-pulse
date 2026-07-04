namespace ResourcePulse.Domain.Events;

// Raised on Allocation.RetargetToDemand when the coverage is re-pointed to another
// demand (amendment C1): same person, other demand. Scaffolded, not dispatched.
public sealed record AllocationRetargeted(
    Guid AllocationId,
    Guid OldDemandId,
    Guid NewDemandId,
    DateTimeOffset OccurredAt) : IDomainEvent;
