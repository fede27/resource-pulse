using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;

namespace ResourcePulse.Services.Plan;

public enum PlanChangeKind
{
    Created = 0,
    Modified,
    Deleted,
    // Not applied: a candidate the caller must choose between (coverInferred
    // ambiguity, amendment C3). Carried in DemandChanges with nothing committed.
    Candidate
}

// The structural consequence of one block under a command. Projected directly
// from the in-memory aggregate (ADR-0018 §2) — no ResolvedHours / name
// enrichment, so it is computable in dryRun without touching the DB. For
// Deleted, the fields reflect the pre-delete state.
public sealed class PlanBlockChange
{
    public PlanChangeKind Kind { get; init; }
    public Guid Id { get; init; }

    // Coverage always has a resource and a demand (Phase 5.1, ADR-0025). Node is
    // denormalized == Demand.ProjectNodeId.
    public Guid DemandId { get; init; }
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public AllocationStatus Status { get; init; }
    public string? Notes { get; init; }
}

// The structural consequence of one demand under a command. Two typed lists
// (blocks + demands) keep the intent explicit — no anonymous upsert (ADR-0018).
// Kind = Candidate marks an ambiguous coverInferred target (amendment C3): the
// row is a choice offered to the caller, not a change that was applied.
public sealed class PlanDemandChange
{
    public PlanChangeKind Kind { get; init; }
    public Guid Id { get; init; }
    public Guid ProjectNodeId { get; init; }
    public Guid RoleId { get; init; }
    public TimeSpan? RequiredHours { get; init; }
    public DemandProvenance Provenance { get; init; }
    public Guid? OwnerResourceId { get; init; }
    public string? Notes { get; init; }
}

// Result of a plan command (ADR-0018). `CommandKind` echoes the intent;
// `Committed` is false in dryRun; `Changes` are the created/modified/deleted
// coverage blocks and `DemandChanges` the demands (created/modified/deleted, or
// Candidate rows for an ambiguous coverInferred) as they would be (dryRun) or are
// (committed).
public sealed class PlanCommandResult
{
    public string CommandKind { get; init; } = string.Empty;
    public bool DryRun { get; init; }
    public bool Committed { get; init; }
    public IReadOnlyList<PlanBlockChange> Changes { get; init; } = [];
    public IReadOnlyList<PlanDemandChange> DemandChanges { get; init; } = [];
}
