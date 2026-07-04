using ResourcePulse.Domain.Demands;

namespace ResourcePulse.Domain.Events;

// Raised by the Demand factory. Scaffolded, not dispatched (ADR-0004).
public sealed record DemandCreated(
    Guid DemandId,
    Guid ProjectNodeId,
    Guid RoleId,
    TimeSpan? RequiredHours,
    DemandProvenance Provenance,
    Guid? OwnerResourceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
