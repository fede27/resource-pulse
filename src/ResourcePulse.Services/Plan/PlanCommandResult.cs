using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Plan;

public enum PlanChangeKind
{
    Created = 0,
    Modified,
    Deleted
}

// The structural consequence of one block under a command. Projected directly
// from the in-memory aggregate (ADR-0018 §2) — no ResolvedHours / name
// enrichment, so it is computable in dryRun without touching the DB. For
// Deleted, the fields reflect the pre-delete state.
public sealed class PlanBlockChange
{
    public PlanChangeKind Kind { get; init; }
    public Guid Id { get; init; }

    public Guid? ResourceId { get; init; }
    public bool IsPlaceholder { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public AllocationStatus Status { get; init; }
    // Open role from the Role catalogue (ADR-0021 / M2).
    public Guid? RoleId { get; init; }
    public Guid? OwnerResourceId { get; init; }
    public string? Notes { get; init; }
}

// Result of a plan command (ADR-0018). `CommandKind` echoes the intent;
// `Committed` is false in dryRun; `Changes` are the created/modified/deleted
// blocks as they would be (dryRun) or are (committed).
public sealed class PlanCommandResult
{
    public string CommandKind { get; init; } = string.Empty;
    public bool DryRun { get; init; }
    public bool Committed { get; init; }
    public IReadOnlyList<PlanBlockChange> Changes { get; init; } = [];
}
