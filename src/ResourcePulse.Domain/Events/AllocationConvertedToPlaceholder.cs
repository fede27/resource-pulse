namespace ResourcePulse.Domain.Events;

// Raised on Allocation.ConvertToPlaceholder(...). Distinct from AllocationDeleted:
// the row stays, the span keeps its identity, the resource pointer is dropped
// and a role/owner reference takes its place (ADR-0016). AllocationDeleted is
// reserved for cancellation effectiva — see ADR-0012.
public sealed record AllocationConvertedToPlaceholder(
    Guid AllocationId,
    Guid OldResourceId,
    Guid ProjectNodeId,
    Guid RoleSkillId,
    Guid? OwnerResourceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
