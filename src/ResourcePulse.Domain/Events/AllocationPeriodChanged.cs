namespace ResourcePulse.Domain.Events;

public sealed record AllocationPeriodChanged(
    Guid AllocationId,
    DateOnly OldStart,
    DateOnly OldEnd,
    DateOnly NewStart,
    DateOnly NewEnd,
    DateTimeOffset OccurredAt) : IDomainEvent;
