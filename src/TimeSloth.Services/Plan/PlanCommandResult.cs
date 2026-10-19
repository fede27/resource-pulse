using TimeSloth.Domain.Allocations;
using TimeSloth.Domain.Demands;
using TimeSloth.Domain.Projects;

namespace TimeSloth.Services.Plan;

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
    // What the gesture does beyond the dates (ADR-0036). Priced only for the
    // kinds that move blocks nobody touched — replanNode / moveSubtree — and
    // null for every other kind.
    public PlanConsequence? Consequence { get; init; }
}

// The consequence of a node move in the currencies the dates do not show
// (ADR-0036): the hours each person gains or loses, the rate% they end up at,
// and what that does to the demands. Computed by the SAME command in dryRun and
// on commit, before anything is saved, so the estimate and the receipt cannot
// disagree.
//
// It carries FACTS and three flags. The flags are the state changes the
// confirmation is built from — the gesture reaches the frozen zone, a person
// changes band, a demand changes state — and they are decided here, once, so
// the board and a future consumer cannot disagree about what counts. Everything
// else (hours that cross no boundary, a gap that shrinks and stays a gap) is in
// the same payload for the account, and asks for nothing.
public sealed class PlanConsequence
{
    public DateOnly Today { get; init; }
    public DateOnly FrozenUntil { get; init; }
    // Some changed day, or a moved node boundary, falls in [Today, FrozenUntil].
    public bool TouchesFrozen { get; init; }
    public IReadOnlyList<PlanBlockShift> Blocks { get; init; } = [];
    public IReadOnlyList<PlanPersonConsequence> People { get; init; } = [];
    public IReadOnlyList<PlanDemandConsequence> Demands { get; init; } = [];
}

// One dragged block, window before and after. PlanBlockChange carries only the
// "after"; the account needs both.
public sealed class PlanBlockShift
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public Guid DemandId { get; init; }
    public decimal AllocationPercent { get; init; }
    public AllocationStatus Status { get; init; }
    public DateOnly OldStart { get; init; }
    public DateOnly OldEnd { get; init; }
    public DateOnly NewStart { get; init; }
    public DateOnly NewEnd { get; init; }
    // Signed: % x capacity over the days gained or lost.
    public TimeSpan HoursDelta { get; init; }
}

public sealed class PlanPersonConsequence
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public TimeSpan HoursDelta { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public DateOnly? AvailableUntil { get; init; }
    public IReadOnlyList<PlanLoadInterval> Intervals { get; init; } = [];
    public IReadOnlyList<PlanZeroCapacityRun> ZeroCapacity { get; init; } = [];
    public PlanBeyondAvailability? BeyondAvailability { get; init; }
}

// A run of days on which the person's committed rate% changed, evaluated day by
// day across ALL their projects. Days without capacity are skipped, not counted
// as zero: their utilization is undefined.
public sealed class PlanLoadInterval
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public decimal PercentBefore { get; init; }
    public decimal PercentAfter { get; init; }
    // The two percentages resolve to different configured load bands.
    public bool BandChanged { get; init; }
    // PercentAfter reaches the last band (LoadBandConfiguration.OverloadFloor).
    public bool OverloadedAfter { get; init; }
    // Projects other than the one being moved that load the same days — the
    // ones not on screen.
    public IReadOnlyList<PlanProjectRef> OtherProjects { get; init; } = [];
}

public sealed class PlanProjectRef
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class PlanZeroCapacityRun
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class PlanBeyondAvailability
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int Days { get; init; }
    public TimeSpan Hours { get; init; }
}

// The reconciliation of one demand before and after, over the whole extent of
// its coverage (not a board range). Best-effort demands carry null gaps and can
// never change state: no target, no gap (ADR-0026).
public sealed class PlanDemandConsequence
{
    public Guid DemandId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public string NodeName { get; init; } = string.Empty;
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public TimeSpan? RequiredHours { get; init; }
    public TimeSpan CoveredBefore { get; init; }
    public TimeSpan CoveredAfter { get; init; }
    public TimeSpan? GapBefore { get; init; }
    public TimeSpan? GapAfter { get; init; }
    // Covered (gap <= 0) on one side and uncovered (gap > 0) on the other.
    public bool StateChanged { get; init; }
}
