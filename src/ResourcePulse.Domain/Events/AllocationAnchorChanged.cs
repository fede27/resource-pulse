using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Domain.Events;

// Raised when one boundary of a block changes what it is tied to (ADR-0034 §7):
// anchoring ("setAnchor", or an anchor given at creation), pinning explicitly,
// or — the common case — an anchor BROKEN by a gesture that moved the edge
// without going through its referent (edit/resize/move/shiftFrom/split/
// reassign/retarget). Reason carries which gesture did it: the "who broke the
// anchor, when" of §9. Scaffolded, not dispatched (ADR-0004).
public sealed record AllocationAnchorChanged(
    Guid AllocationId,
    BoundaryEdge Edge,
    BoundaryAnchor OldAnchor,
    BoundaryAnchor NewAnchor,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
