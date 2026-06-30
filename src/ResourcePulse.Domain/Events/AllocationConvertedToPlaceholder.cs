namespace ResourcePulse.Domain.Events;

// Raised on Allocation.ConvertToPlaceholder(...). Distinct from AllocationDeleted:
// the row stays, the span keeps its identity, the resource pointer is dropped
// and a role/owner reference takes its place (ADR-0016). RoleId targets the Role
// catalogue (ADR-0021 / M2). AllocationDeleted is reserved for cancellation
// effectiva — see ADR-0012.
public sealed record AllocationConvertedToPlaceholder(
    Guid AllocationId,
    Guid OldResourceId,
    Guid ProjectNodeId,
    Guid RoleId,
    Guid? OwnerResourceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
