namespace ResourcePulse.Domain.Events;

public sealed record ProjectNodeDeleted(
    Guid NodeId,
    string Path,
    DateTimeOffset OccurredAt) : IDomainEvent;
