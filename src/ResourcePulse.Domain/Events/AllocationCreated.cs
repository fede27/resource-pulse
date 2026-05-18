namespace ResourcePulse.Domain.Events;

public sealed record AllocationCreated(
    Guid AllocationId,
    Guid ResourceId,
    Guid ProjectNodeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal AllocationPercent,
    DateTimeOffset OccurredAt) : IDomainEvent;
