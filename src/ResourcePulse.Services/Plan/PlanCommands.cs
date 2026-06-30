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
[JsonDerivedType(typeof(CreatePlaceholderCommand), "createPlaceholder")]
[JsonDerivedType(typeof(EditCommand), "edit")]
[JsonDerivedType(typeof(SplitAtCommand), "splitAt")]
[JsonDerivedType(typeof(ChangeRateFromCommand), "changeRateFrom")]
[JsonDerivedType(typeof(MoveCommand), "move")]
[JsonDerivedType(typeof(RetargetCommand), "retarget")]
[JsonDerivedType(typeof(ResizeCommand), "resize")]
[JsonDerivedType(typeof(ShiftFromCommand), "shiftFrom")]
[JsonDerivedType(typeof(ConvertToPlaceholderCommand), "convertToPlaceholder")]
[JsonDerivedType(typeof(ReassignCommand), "reassign")]
[JsonDerivedType(typeof(ChangeStatusCommand), "changeStatus")]
[JsonDerivedType(typeof(DeleteCommand), "delete")]
public abstract class PlanCommand
{
    // When true: compute the consequence and return it without persisting.
    public bool DryRun { get; init; }
}

// ── Creation ──────────────────────────────────────────────────────────────

// Rate-shaped assigned creation ("this resource at X% for this window").
public sealed class CreateCommand : PlanCommand
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Percent { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
}

// Quantity-shaped assigned creation ("Y hours over this window"). Resolved to a
// percent via AllocationResolver using the resource's window capacity.
public sealed class CreateByHoursCommand : PlanCommand
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public TimeSpan TargetHours { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
}

// Open-role creation (ADR-0016). Rate-shaped only (no resource ⇒ no capacity).
public sealed class CreatePlaceholderCommand : PlanCommand
{
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Percent { get; init; }
    // Open role from the Role catalogue (ADR-0021 / M2), not Skill.
    public Guid RoleId { get; init; }
    public Guid? OwnerResourceId { get; init; }
    public AllocationStatus Status { get; init; } = AllocationStatus.Tentative;
    public string? Notes { get; init; }
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

// Retarget to an explicit window; user picks which dimension to preserve
// (ADR-0013). KeepHours rebalances percent via capacity.
public sealed class RetargetCommand : PlanCommand
{
    public Guid Id { get; init; }
    public DateOnly NewPeriodStart { get; init; }
    public DateOnly NewPeriodEnd { get; init; }
    public MoveMode Mode { get; init; }
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

// ── Form & status transitions ─────────────────────────────────────────────

// Assigned → placeholder (ADR-0016).
public sealed class ConvertToPlaceholderCommand : PlanCommand
{
    public Guid Id { get; init; }
    // Open role from the Role catalogue (ADR-0021 / M2), not Skill.
    public Guid RoleId { get; init; }
    public Guid? OwnerResourceId { get; init; }
}

// Placeholder → assigned (ADR-0016).
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

// Actual cancellation (row leaves the collection, ADR-0012). Distinct from
// convertToPlaceholder (which keeps the demand as an open role).
public sealed class DeleteCommand : PlanCommand
{
    public Guid Id { get; init; }
}

// Which dimension a retarget preserves (ADR-0013). Relocated from the retired
// allocation DTOs into the plan namespace.
public enum MoveMode
{
    KeepPercent = 0,
    KeepHours
}
