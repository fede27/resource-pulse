namespace TimeSloth.Domain.Events;

public sealed record ProjectNodeReparented(
    Guid NodeId,
    Guid? OldParentId,
    Guid? NewParentId,
    DateTimeOffset OccurredAt) : IDomainEvent;
