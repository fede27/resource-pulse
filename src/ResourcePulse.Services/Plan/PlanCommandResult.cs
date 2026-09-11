using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Services.Allocations;

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

    // Boundary semantics (ADR-0034). Structural — no referent name here.
    public BoundaryAnchorDto StartAnchor { get; init; } = new();
    public BoundaryAnchorDto EndAnchor { get; init; } = new();
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

// What kind of referent moved (ADR-0034 §5).
public enum ReferentKind
{
    Node = 0,
    Resource = 1,
    ExternalConstraint = 2
}

// The structural consequence of one REFERENT under a referent-moving command
// (ADR-0034 §5): its window before and after. For a node the window is
// PlannedStart/End; for a resource AvailableFrom/Until; for an external
// constraint the single date sits in both Start and End. Name and type are
// carried so a preview can say "Fase 2: 8 Jun -> 15 Jun" without a lookup.
public sealed class PlanReferentChange
{
    public PlanChangeKind Kind { get; init; }
    public ReferentKind Referent { get; init; }
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    // Only for Referent = Node.
    public ProjectNodeType? NodeType { get; init; }
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
    // Referents moved by replanNode / moveSubtree / setAvailability /
    // moveConstraint (ADR-0034). Empty for every other kind. The dragged blocks
    // are in Changes; their count IS the confirmation.
    public IReadOnlyList<PlanReferentChange> ReferentChanges { get; init; } = [];
}
