namespace ResourcePulse.Domain.Events;

public sealed record ProjectBaselined(
    Guid NodeId,
    DateOnly Start,
    DateOnly End,
    bool IsRebaseline,
    DateTimeOffset OccurredAt) : IDomainEvent;
