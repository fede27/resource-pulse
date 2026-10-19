using TimeSloth.Domain.Projects;

namespace TimeSloth.Domain.Events;

public sealed record ProjectStatusChanged(
    Guid NodeId,
    ProjectStatus From,
    ProjectStatus To,
    DateTimeOffset OccurredAt) : IDomainEvent;
