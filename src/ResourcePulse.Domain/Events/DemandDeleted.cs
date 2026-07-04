namespace ResourcePulse.Domain.Events;

// Raised by Demand.MarkDeleted(), called by the service layer just before
// repository.Remove (mirrors AllocationDeleted / ADR-0012). A demand can only be
// deleted once it has no coverage referencing it (FK Restrict).
public sealed record DemandDeleted(
    Guid DemandId,
    DateTimeOffset OccurredAt) : IDomainEvent;
