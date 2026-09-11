namespace ResourcePulse.Domain.Allocations;

// What a block boundary is tied to (ADR-0034 §1). Pinned is the absence of a
// tie — an absolute date that moves only when someone edits it. Every other
// value names a REFERENT whose date the boundary carries (I9): the boundary
// moves when the referent moves, and only then.
//
// NodeStart / NodeEnd are two kinds, not one kind plus an edge selector, and
// they are independent of which edge of the block they sit on: "start when
// phase 1 ends" is a START boundary anchored to NodeEnd.
public enum AnchorKind
{
    Pinned = 0,

    // Referent: a ProjectNode (Project|Phase) — its PlannedStart / PlannedEnd.
    NodeStart = 1,
    NodeEnd = 2,

    // Referent: the block's own resource — AvailableFrom on a start edge,
    // AvailableUntil on an end edge. Implicit; no referent column.
    ResourceAvailability = 3,

    // Referent: an ExternalConstraint (an imposed date on the root project).
    External = 4
}
