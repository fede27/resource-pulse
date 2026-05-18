namespace ResourcePulse.Domain.Events;

public sealed record ProjectReplanned(
    Guid NodeId,
    DateOnly? Start,
    DateOnly? End,
    DateTimeOffset OccurredAt) : IDomainEvent;
