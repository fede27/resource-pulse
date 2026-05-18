namespace ResourcePulse.Domain.Events;

public sealed record ResourceTeamChanged(
    Guid ResourceId,
    Guid? OldTeamId,
    Guid? NewTeamId,
    DateTimeOffset OccurredAt) : IDomainEvent;
