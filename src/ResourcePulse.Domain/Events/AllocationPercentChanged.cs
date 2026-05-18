namespace ResourcePulse.Domain.Events;

public sealed record AllocationPercentChanged(
    Guid AllocationId,
    decimal OldPercent,
    decimal NewPercent,
    DateTimeOffset OccurredAt) : IDomainEvent;
