using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;

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

    // Boundary state (ADR-0035): FollowsNode or Pinned per edge. The referent
    // needs no name here — it is ProjectNodeId, already on this row.
    public AnchorKind StartAnchor { get; init; }
    public AnchorKind EndAnchor { get; init; }
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
    // The EXPLICIT decision deadline (ADR-0033 §3); null means "derive it".
    public DateOnly? DecideBy { get; init; }
}

// The structural consequence of the moved REFERENT under replanNode /
// moveSubtree (ADR-0035 §5): its planned window before and after. There is one
// referent type since ADR-0035 — the project node — so there is no discriminator
// to carry. Name and type are carried so a preview can say
// "Fase 2 · Build: 30 ago -> 9 set" without a lookup.
public sealed class PlanReferentChange
{
    public PlanChangeKind Kind { get; init; }
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ProjectNodeType NodeType { get; init; }
    public DateOnly? OldStart { get; init; }
    public DateOnly? OldEnd { get; init; }
    public DateOnly? NewStart { get; init; }
    public DateOnly? NewEnd { get; init; }
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
    // Nodes moved by replanNode / moveSubtree (ADR-0035). Empty for every other
    // kind. The dragged blocks are in Changes; their count IS the confirmation.
    public IReadOnlyList<PlanReferentChange> ReferentChanges { get; init; } = [];
}
