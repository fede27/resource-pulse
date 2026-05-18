using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Domain.Events;

public sealed record ProjectStatusChanged(
    Guid NodeId,
    ProjectStatus From,
    ProjectStatus To,
    DateTimeOffset OccurredAt) : IDomainEvent;
