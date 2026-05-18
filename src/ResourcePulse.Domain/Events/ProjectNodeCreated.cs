using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Domain.Events;

public sealed record ProjectNodeCreated(
    Guid NodeId,
    Guid? ParentId,
    ProjectNodeType NodeType,
    DateTimeOffset OccurredAt) : IDomainEvent;
