using System.Text.Json.Serialization;
using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Plan;

// Command envelope for plan mutation (ADR-0018). A single typed edit — a
// discriminated union with an explicit string discriminator ("kind"). The edit
// TYPE is the intent: it feeds decision-level provenance (§9). One endpoint
// (POST /api/plan/commands) dispatches on the runtime type.
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
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
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
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
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
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
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
