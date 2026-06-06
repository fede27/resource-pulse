namespace ResourcePulse.Domain.Events;

// Raised on Allocation.AssignTo(...). Inverse transition of
// AllocationConvertedToPlaceholder: same aggregate Id, same span, same rate%,
// resource pointer becomes valued, placeholder fields cleared (ADR-0016).
public sealed record PlaceholderAssignedToResource(
    Guid AllocationId,
    Guid NewResourceId,
    Guid ProjectNodeId,
    DateTimeOffset OccurredAt) : IDomainEvent;
