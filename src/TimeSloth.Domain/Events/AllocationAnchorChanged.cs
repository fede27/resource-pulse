using TimeSloth.Domain.Allocations;

namespace TimeSloth.Domain.Events;

// Raised when one boundary of a block changes state (ADR-0035 §7): pinned by a
// gesture that moved it (edit/resize/move/shiftFrom/split), pinned explicitly,
// or re-attached to its node. Reason carries which gesture did it: the "who
// broke the tie, when" of ADR-0034 §9. Scaffolded, not dispatched (ADR-0004).
public sealed record AllocationAnchorChanged(
    Guid AllocationId,
    BoundaryEdge Edge,
    AnchorKind OldAnchor,
    AnchorKind NewAnchor,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
