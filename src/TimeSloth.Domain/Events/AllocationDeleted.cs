namespace TimeSloth.Domain.Events;

public sealed record AllocationDeleted(
    Guid AllocationId,
    DateTimeOffset OccurredAt) : IDomainEvent;
