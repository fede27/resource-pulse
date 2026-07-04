namespace ResourcePulse.Domain.Events;

// Raised on Allocation.Reassign when the covering resource changes (amendment
// C1): swap who covers a demand, same demand and span. Scaffolded, not dispatched.
public sealed record AllocationResourceChanged(
    Guid AllocationId,
    Guid OldResourceId,
    Guid NewResourceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
