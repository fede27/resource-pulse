using System.Text.Json.Serialization;
using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Plan;

// Command envelope for plan mutation (ADR-0018). A single typed edit — a
// discriminated union with an explicit string discriminator ("kind"). The edit
// TYPE is the intent: it feeds decision-level provenance (§9). One endpoint
// (POST /api/plan/commands) dispatches on the runtime type.
//
// ADR-0035 removed two kinds — setAvailability and moveConstraint. Neither a
// person's declared availability nor an imposed external date is a referent
// any more, so moving one moves no block and is an ordinary anagrafica edit
// (PUT /api/resources/{id}, PUT /api/projects/{rootId}/constraints/{id}).
//
// Every command carries DryRun: when true the handler computes the consequence
// (PlanBlockChange[]) WITHOUT committing (ADR-0018 §2). The mechanism is uniform
// across all commands present and future (anchoring gestures will arrive as new
// kinds and reuse the same DryRun).
//
// Wire form example:
//   { "kind": "splitAt", "dryRun": true, "id": "…", "date": "2026-06-08" }
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", IgnoreUnrecognizedTypeDiscriminators = false,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CreateCommand), "create")]
[JsonDerivedType(typeof(CreateByHoursCommand), "createByHours")]
[JsonDerivedType(typeof(CoverInferredCommand), "coverInferred")]
[JsonDerivedType(typeof(EditCommand), "edit")]
[JsonDerivedType(typeof(SplitAtCommand), "splitAt")]
[JsonDerivedType(typeof(ChangeRateFromCommand), "changeRateFrom")]
[JsonDerivedType(typeof(MoveCommand), "move")]
[JsonDerivedType(typeof(RetargetCommand), "retarget")]
[JsonDerivedType(typeof(ResizeCommand), "resize")]
[JsonDerivedType(typeof(ShiftFromCommand), "shiftFrom")]
[JsonDerivedType(typeof(ReassignCommand), "reassign")]
[JsonDerivedType(typeof(ChangeStatusCommand), "changeStatus")]
[JsonDerivedType(typeof(DeleteCommand), "delete")]
[JsonDerivedType(typeof(CreateDemandCommand), "createDemand")]
[JsonDerivedType(typeof(EditDemandCommand), "editDemand")]
[JsonDerivedType(typeof(DeleteDemandCommand), "deleteDemand")]
[JsonDerivedType(typeof(FollowNodeCommand), "followNode")]
[JsonDerivedType(typeof(PinCommand), "pin")]
[JsonDerivedType(typeof(ReplanNodeCommand), "replanNode")]
[JsonDerivedType(typeof(MoveSubtreeCommand), "moveSubtree")]
public abstract class PlanCommand
{
    // When true: compute the consequence and return it without persisting.
    public bool DryRun { get; init; }
}

// ── Demand (Phase 5.0) ──────────────────────────────────────────────────────

// Declare a demand (revision §4). Provenance is always Declared here — the
// Inferred path is coverInferred (Phase 5.1). RequiredHours null => best-effort.
public sealed class CreateDemandCommand : PlanCommand
{
    public Guid ProjectNodeId { get; init; }
    public Guid RoleId { get; init; }
    public TimeSpan? RequiredHours { get; init; }
    public Guid? OwnerResourceId { get; init; }
    public string? Notes { get; init; }

    // The EXPLICIT decision deadline (ADR-0033 §3) — an override. Null is the
    // normal case and means "derive it" from the node's planned start minus the
    // org lead time. A new field on an existing kind, not a new kind: the gesture
    // is still "create a demand" (ADR-0018).
    public DateOnly? DecideBy { get; init; }
}

// Edit a demand. RoleId (optional) corrects the role via Demand.ChangeRole
// (amendment C2) — the intent stays "edit demand", not a new kind. ProjectNodeId
// is never editable (a demand on another node is another demand). Nullable fields
// use *Set flags so "leave unchanged" is distinguishable from "clear to null".
public sealed class EditDemandCommand : PlanCommand
{
    public Guid Id { get; init; }

    public Guid? RoleId { get; init; }

    public TimeSpan? RequiredHours { get; init; }
    public bool RequiredHoursSet { get; init; }

    public Guid? OwnerResourceId { get; init; }
    public bool OwnerResourceIdSet { get; init; }

    public string? Notes { get; init; }
    public bool NotesSet { get; init; }

    // Clearing it (Set=true, value=null) returns the demand to the DERIVED
    // deadline — it does not remove urgency (ADR-0033 §3).
    public DateOnly? DecideBy { get; init; }
    public bool DecideBySet { get; init; }
}

// Delete a demand. Fails Conflict if any coverage references it (FK Restrict) —
// detach the coverage first.
public sealed class DeleteDemandCommand : PlanCommand
{
    public Guid Id { get; init; }
}

// ── Creation ──────────────────────────────────────────────────────────────

// Rate-shaped coverage creation against an existing demand ("this resource at X%
// covering this demand"). The node is read from the demand (I8) — not accepted here.
public sealed class CreateCommand : PlanCommand
{
    public Guid DemandId { get; init; }
    public Guid ResourceId { get; init; }
    // The window. BOTH ENDS ARE OPTIONAL and null is the normal case (ADR-0035
    // §2): a null edge FOLLOWS the node — it takes the node's planned boundary
    // and keeps moving with it. A supplied date PINS that edge. So "cover this
    // demand at 60%" needs no dates at all, and "…but only until 16 Aug" needs
    // exactly one. Conflict if an edge must follow a node that has no such
    // planned date: the block would have no boundary to take.
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }
    public decimal Percent { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
}

// Quantity-shaped coverage creation ("Y hours over this window"). Resolved to a
// percent via AllocationResolver using the resource's window capacity.
public sealed class CreateByHoursCommand : PlanCommand
{
    public Guid DemandId { get; init; }
    public Guid ResourceId { get; init; }
    // The window. BOTH ENDS ARE OPTIONAL and null is the normal case (ADR-0035
    // §2): a null edge FOLLOWS the node — it takes the node's planned boundary
    // and keeps moving with it. A supplied date PINS that edge. So "cover this
    // demand at 60%" needs no dates at all, and "…but only until 16 Aug" needs
    // exactly one. Conflict if an edge must follow a node that has no such
    // planned date: the block would have no boundary to take.
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }
    public TimeSpan TargetHours { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
}

// Demand-from-gesture coverage (revision §5, amendment C3 — attach-first). "Just
// drop this resource on this node+role": if an uncovered demand already exists on
// (node, role) the service covers it (provenance unchanged); if none exists it
// materializes an Inferred demand; if more than one uncovered candidate exists it
// returns the candidate list and commits nothing.
public sealed class CoverInferredCommand : PlanCommand
{
    public Guid ProjectNodeId { get; init; }
    public Guid RoleId { get; init; }
    public Guid ResourceId { get; init; }
    // The window. BOTH ENDS ARE OPTIONAL and null is the normal case (ADR-0035
    // §2): a null edge FOLLOWS the node — it takes the node's planned boundary
    // and keeps moving with it. A supplied date PINS that edge. So "cover this
    // demand at 60%" needs no dates at all, and "…but only until 16 Aug" needs
    // exactly one. Conflict if an edge must follow a node that has no such
    // planned date: the block would have no boundary to take.
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }
    public decimal Percent { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
    // Owner seeded onto the demand when the fallback materializes an Inferred one.
    public Guid? OwnerResourceId { get; init; }
}

// ── Edit in place ─────────────────────────────────────────────────────────

// In-place edit (no rebalance): period + percent + notes. Was PUT /{id}.
public sealed class EditCommand : PlanCommand
{
    public Guid Id { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public string? Notes { get; init; }
}

// ── Span operations (kernel — ADR-0019) ───────────────────────────────────

// Structural, non-destructive cut at `date` into two adjacent blocks.
public sealed class SplitAtCommand : PlanCommand
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
}

// SplitAt(date) + ChangePercent(newRate) on the second block.
public sealed class ChangeRateFromCommand : PlanCommand
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public decimal NewRate { get; init; }
}

// Translate one block by `deltaDays` (may be negative). Rate & duration preserved.
public sealed class MoveCommand : PlanCommand
{
    public Guid Id { get; init; }
    public int DeltaDays { get; init; }
}

// Re-point the coverage to ANOTHER demand (amendment C1): same person, other
// demand. The node is re-read from the target demand (I8). Span/percent/status/
// resource unchanged. (Was the window-rebalance retarget; MoveMode is retired.)
public sealed class RetargetCommand : PlanCommand
{
    public Guid Id { get; init; }
    public Guid DemandId { get; init; }
}

// Move a single edge to an explicit date; the other, if null, stays fixed.
public sealed class ResizeCommand : PlanCommand
{
    public Guid Id { get; init; }
    public DateOnly? NewPeriodStart { get; init; }
    public DateOnly? NewPeriodEnd { get; init; }
}

// Cascade on the lane (resource × project_node): translate all blocks with
// start ≥ fromDate by deltaDays, preserving relative gaps (§7).
public sealed class ShiftFromCommand : PlanCommand
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly FromDate { get; init; }
    public int DeltaDays { get; init; }
}

// ── Resource & status transitions ─────────────────────────────────────────

// Swap the covering resource on the SAME demand (amendment C1): other person,
// same demand. Orthogonal to retarget (same person, other demand).
public sealed class ReassignCommand : PlanCommand
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
}

// Promote/demote commitment status (ADR-0015). I6 gated at the service.
public sealed class ChangeStatusCommand : PlanCommand
{
    public Guid Id { get; init; }
    public AllocationStatus Status { get; init; }
    public string? Reason { get; init; }
}

// Deallocation (amendment C1 / revision §8): the coverage leaves the collection
// (ADR-0012) and the demand underneath re-surfaces as uncovered. This is now the
// canonical "remove the person" — there is no convert-to-placeholder anymore.
public sealed class DeleteCommand : PlanCommand
{
    public Guid Id { get; init; }
}

// ── Boundaries (ADR-0035) ─────────────────────────────────────────────────
//
// Two kinds, because there is nothing to point at: the referent of a boundary
// is the node its coverage already sits on. On the board these are one gesture
// each — drag an edge inward and it pins, drag it back onto the node's boundary
// and it follows again — and in the drawer they are the two ends of one switch.

// Re-attach one edge to its node and SNAP it to the node's date. The only way
// to express "this ends when the phase ends" once the block already exists.
// Conflict if the node has no such planned date, or if the snap would invert
// the span.
public sealed class FollowNodeCommand : PlanCommand
{
    public Guid Id { get; init; }
    public BoundaryEdge Edge { get; init; }
}

// Release one edge WITHOUT moving it. Explicit on purpose: "it ends 30 Aug even
// if the phase stretches" is a decision with a name, and it is the one thing a
// drag on the board cannot say — the date it would land on is the node's own.
public sealed class PinCommand : PlanCommand
{
    public Guid Id { get; init; }
    public BoundaryEdge Edge { get; init; }
}

// ── Moving the node (ADR-0035 §5) ─────────────────────────────────────────
//
// The node is the ONLY referent, and therefore the only surface that can drag
// blocks. It is not replanned from the anagrafica when something follows it: it
// moves through the envelope, and dryRun is the countable confirmation ("move
// the N coverages that follow it as well?"). The dragged blocks come back in
// Changes as Modified, the node in ReferentChanges.

// Set ONE node's planned dates; every boundary of a coverage ON that node that
// still follows it re-snaps (I9). Clearing a date that following boundaries
// take is a Conflict with the count — pin them first. A node nothing follows can
// still be replanned from the project endpoint; this kind is for the case where
// something moves with it.
public sealed class ReplanNodeCommand : PlanCommand
{
    public Guid NodeId { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedEnd { get; init; }
}

// Translate the planned dates of a node AND its whole subtree by DeltaDays;
// every boundary following any of them re-snaps. This is the "spostare una
// fase" of §8 — the subtree cascade ADR-0019 deferred — and the name the
// earlier ADRs promised. No propagation between sibling phases: moving Phase 2
// does not move Phase 3, and the overlap that results is legal and sums.
public sealed class MoveSubtreeCommand : PlanCommand
{
    public Guid NodeId { get; init; }
    public int DeltaDays { get; init; }
}
