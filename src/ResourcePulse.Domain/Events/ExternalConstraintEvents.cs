using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Domain.Events;

// External constraint lifecycle (ADR-0034 §4). Scaffolded, not dispatched
// (ADR-0004). Moved is the one with provenance value: an imposed date that
// moves is a renegotiation, and the plan's anchored boundaries follow it.
public sealed record ExternalConstraintCreated(
    Guid ConstraintId,
    Guid RootProjectId,
    DateOnly Date,
    ConstraintAuthority Authority,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ExternalConstraintMoved(
    Guid ConstraintId,
    DateOnly OldDate,
    DateOnly NewDate,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ExternalConstraintDeleted(
    Guid ConstraintId,
    DateTimeOffset OccurredAt) : IDomainEvent;
