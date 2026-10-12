namespace ResourcePulse.Domain.Allocations;

// What ONE boundary of a coverage block is (ADR-0035, superseding ADR-0034 §1).
//
// The referent is not chosen: it is already there. A coverage is born on a
// demand, the demand lives on a Project/Phase node, and THAT node is the only
// referent a boundary can have. So a boundary has two states, not five, and
// there is nothing to point at:
//
//   FollowsNode — the edge coincides with the corresponding boundary of its own
//                 node (start edge -> PlannedStart, end edge -> PlannedEnd) and
//                 moves when the node moves. The default, and silent: a board
//                 at rest is a board of blocks.
//   Pinned      — someone decided a date and that date stays. The exception,
//                 and the only thing marked on the board.
//
// Which node boundary an edge follows is a function of WHICH EDGE it is, so the
// old NodeStart/NodeEnd pair collapses into one value. "Start when phase 1
// ends" is no longer expressible — deliberately: see ADR-0035 §4 for the three
// capabilities traded away and what is done instead.
public enum AnchorKind
{
    Pinned = 0,
    FollowsNode = 1
}
