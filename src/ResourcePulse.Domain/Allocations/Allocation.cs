using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Allocations;

// A COVERAGE (Phase 5.1, ADR-0025): a real resource committed to a DEMAND over an
// inclusive date window, at a given percentage of the resource's capacity.
// Aggregate root with its own lifecycle.
//
// Core revision (revision §8, §10): the demand is the native, first-class object;
// the coverage is what sits on top of it. An Allocation therefore ALWAYS has a
// real resource and points at a Demand — there is no placeholder state anymore
// (the old ADR-0016 form XOR / I7 is retired). Uncovered work is a Demand with no
// coverage, not a degraded Allocation.
//
//   DemandId       — the demand this coverage covers (the FK of record).
//   ProjectNodeId  — DENORMALIZED, always == Demand.ProjectNodeId. Set by the
//                    service from the demand, never accepted from the client (I8).
//                    Kept so the per-node / subtree / Path-prefix load queries
//                    stay cheap.
//   ResourceId     — the covering person (required, non-null again).
//
// The role lives on the Demand, never here (§6): a coverage whose resource's role
// differs from the demand's role is a visible mismatch, surfaced, not enforced.
//
// Status di impegno (ADR-0015):
//   - Tentative (default): planning hypothesis, always free.
//   - Hard: commitment requiring grounding. I6 (service-level) admits Hard only
//     if the root Project's CommitmentLevel is hard-committed (CommitmentPolicy).
//
// Unit (ADR-0026): stored in percent; hours are the reconciliation truth
// (% × capacity), derived, not stored.
//
// Boundaries (ADR-0035): each edge is either FollowsNode — it coincides with
// the corresponding boundary of THIS coverage's own node (start -> PlannedStart,
// end -> PlannedEnd) and moves with it — or Pinned, an absolute date. There is
// no referent to choose: the node is the one the demand lives on (I8), so the
// tie is a boolean, not a reference. PeriodStart/PeriodEnd stay the stored,
// denormalized dates (calculators and read-models never look at the anchor).
// The one rule: an edge moved by anything but its node's propagation becomes
// Pinned (ChangePeriod applies it; FollowNode/FollowNodes keep the tie).
//
// Local invariants only:
//   - period start <= period end
//   - rate% in (0, 1000] — typo safeguard, not a cap (ADR-0013)
//   - demand, node and resource ids non-empty
//
// Cross-aggregate (I3, I4, I6, I8, I9-snap) live in the service layer
// (PlanCommandService). I10 is retired with ADR-0035: there is no referent to
// scope. Overlap on the same (resource, project_node) sums
// (ADR-0014) — never re-checked.
public sealed class Allocation : Entity<Guid>, IAuditable
{
    public Guid DemandId { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid ProjectNodeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal AllocationPercent { get; private set; }
    public AllocationStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public AnchorKind StartAnchor { get; private set; } = AnchorKind.Pinned;
    public AnchorKind EndAnchor { get; private set; } = AnchorKind.Pinned;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Allocation() { }

    // Creates a coverage. Default Status = Tentative (ADR-0015 §1). projectNodeId
    // is passed by the service from the target demand (denormalization, I8).
    // Both edges are born Pinned; the service attaches them afterwards
    // (FollowNode) once it has read the node's planned dates. A creation whose
    // window is left open is the normal case and ends up FollowsNode on both.
    public static Allocation CreateCoverage(
        Guid demandId,
        Guid projectNodeId,
        Guid resourceId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        string? notes = null,
        AllocationStatus status = AllocationStatus.Tentative)
    {
        if (demandId == Guid.Empty)
            throw new DomainException("Coverage must reference a demand.");
        if (resourceId == Guid.Empty)
            throw new DomainException("Coverage must reference a resource.");
        AssertCommonInputs(projectNodeId, periodStart, periodEnd, allocationPercent);
        AssertStatusKnown(status);

        var id = Guid.NewGuid();
        var allocation = new Allocation
        {
            Id = id,
            DemandId = demandId,
            ResourceId = resourceId,
            ProjectNodeId = projectNodeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AllocationPercent = allocationPercent,
            Status = status,
            Notes = NormalizeNotes(notes)
        };

        allocation.RaiseEvent(new AllocationCreated(
            id, demandId, resourceId, projectNodeId, periodStart, periodEnd, allocationPercent, DateTimeOffset.UtcNow));
        return allocation;
    }

    // reason is optional decision-level provenance (ADR-0017), propagated to
    // AllocationPeriodChanged. The span operations (Shift, Resize, cascade) pass
    // it; Edit leaves it null.
    //
    // Pin-on-move (ADR-0035 §6): an edge whose date changes here is being moved
    // by a gesture, not by its node — it stops following. The edge that did not
    // move keeps its state.
    public void ChangePeriod(DateOnly newStart, DateOnly newEnd, string? reason = null)
    {
        var startMoves = PeriodStart != newStart;
        var endMoves = PeriodEnd != newEnd;
        SetPeriod(newStart, newEnd, reason);
        if (startMoves) Pin(BoundaryEdge.Start, reason);
        if (endMoves) Pin(BoundaryEdge.End, reason);
    }

    // ── Boundaries (ADR-0035) ───────────────────────────────────────────────

    // Attaches an edge to its own node and SNAPS it to the node's date (I9).
    // The service reads the date; there is nothing to resolve or scope, because
    // the node is the one this coverage already sits on. The other edge is
    // untouched; if the snap would invert the span the span rule throws and
    // nothing changes.
    public void FollowNode(BoundaryEdge edge, DateOnly nodeDate, string? reason = null)
    {
        AssertEdgeKnown(edge);

        var (newStart, newEnd) = edge == BoundaryEdge.Start
            ? (nodeDate, PeriodEnd)
            : (PeriodStart, nodeDate);
        SetPeriod(newStart, newEnd, reason);
        SetAnchor(edge, AnchorKind.FollowsNode, reason);
    }

    // Releases an edge: it stops following, the date stays where it is. No-op on
    // an already Pinned edge. Explicit on purpose — "fix this date even though it
    // sits exactly on the node's boundary" is a decision with a name, and the one
    // thing a drag on the board cannot express.
    public void Pin(BoundaryEdge edge, string? reason = null)
    {
        AssertEdgeKnown(edge);
        SetAnchor(edge, AnchorKind.Pinned, reason);
    }

    // Propagation (ADR-0035 §5): the node moved, the following edges take its new
    // dates and KEEP following. Re-snap, never a delta: a longer phase makes the
    // block longer. Null = that edge is not following (stays where it is). Both
    // edges move as ONE span change, so a shift larger than the block's own
    // length does not invert it half-way.
    public void FollowNodes(DateOnly? newStart, DateOnly? newEnd, string? reason = null)
    {
        if (newStart is null && newEnd is null) return;
        if (newStart is not null && StartAnchor != AnchorKind.FollowsNode)
            throw new DomainException("The Start boundary is Pinned and does not follow the node.");
        if (newEnd is not null && EndAnchor != AnchorKind.FollowsNode)
            throw new DomainException("The End boundary is Pinned and does not follow the node.");

        SetPeriod(newStart ?? PeriodStart, newEnd ?? PeriodEnd, reason);
    }

    public AnchorKind AnchorOf(BoundaryEdge edge) =>
        edge == BoundaryEdge.Start ? StartAnchor : EndAnchor;

    public bool Follows(BoundaryEdge edge) => AnchorOf(edge) == AnchorKind.FollowsNode;

    // ── Span operations (ADR-0017) ───────────────────────────────────────────

    // SplitAt — structural, NON-destructive. Cuts the span at `date` into two
    // adjacent, NON-overlapping blocks: this becomes [PeriodStart, date-1], the
    // returned sibling covers [date, PeriodEnd]. Both carry the same rate%, status,
    // demand, node and resource. `date` must be strictly interior.
    //
    // Boundaries (ADR-0035 §6): the two edges CREATED by the cut are born Pinned
    // — they are dates a person chose — while the outer edges keep what they had:
    // this block keeps its StartAnchor, the new block inherits the EndAnchor.
    // Sum invariant, boundary states invariant.
    public Allocation SplitAt(DateOnly date, string? reason = null)
    {
        if (date <= PeriodStart || date > PeriodEnd)
            throw new DomainException(
                "Split date must be strictly inside the allocation span (PeriodStart < date <= PeriodEnd).");

        var second = CloneForSplit(date, PeriodEnd, EndAnchor);
        PeriodEnd = date.AddDays(-1);
        SetAnchor(BoundaryEdge.End, AnchorKind.Pinned, reason ?? "Split");

        RaiseEvent(new AllocationSplit(Id, date, second.Id, NormalizeReason(reason), DateTimeOffset.UtcNow));
        return second;
    }

    // ChangeRateFrom — SplitAt(date) + ChangePercent(newRate) on the new block.
    public Allocation ChangeRateFrom(DateOnly date, decimal newRate, string? reason = null)
    {
        var second = SplitAt(date, reason ?? "ChangeRateFrom");
        second.ChangePercent(newRate);
        return second;
    }

    // Shift — translate the whole span by `deltaDays`, preserving duration & rate.
    // Both edges move by a gesture ⇒ both anchors break (ChangePeriod).
    public void Shift(int deltaDays, string? reason = null) =>
        ChangePeriod(PeriodStart.AddDays(deltaDays), PeriodEnd.AddDays(deltaDays), reason ?? "Shift");

    // Resize — move a single edge to an explicit date, leaving the other fixed.
    public void Resize(DateOnly? newStart, DateOnly? newEnd, string? reason = null)
    {
        if (newStart is null && newEnd is null)
            throw new DomainException("Resize requires at least one of newStart or newEnd.");
        ChangePeriod(newStart ?? PeriodStart, newEnd ?? PeriodEnd, reason ?? "Resize");
    }

    // Copies the current coverage into a new aggregate over [start, end],
    // preserving demand, node, resource, rate, status and notes. New Id; no
    // creation event (provenance lives on the AllocationSplit raised by SplitAt).
    // The start edge is the cut and is Pinned; the end edge inherits `endAnchor`.
    private Allocation CloneForSplit(DateOnly start, DateOnly end, AnchorKind endAnchor) =>
        new()
        {
            Id = Guid.NewGuid(),
            DemandId = DemandId,
            ResourceId = ResourceId,
            ProjectNodeId = ProjectNodeId,
            PeriodStart = start,
            PeriodEnd = end,
            AllocationPercent = AllocationPercent,
            Status = Status,
            Notes = Notes,
            StartAnchor = AnchorKind.Pinned,
            EndAnchor = endAnchor
        };

    public void ChangePercent(decimal newPercent)
    {
        AssertPercentInRange(newPercent);
        if (AllocationPercent == newPercent) return; // no-op suppresses event

        var oldPercent = AllocationPercent;
        AllocationPercent = newPercent;
        RaiseEvent(new AllocationPercentChanged(Id, oldPercent, newPercent, DateTimeOffset.UtcNow));
    }

    public void Annotate(string? notes) => Notes = NormalizeNotes(notes);

    public void ChangeStatus(AllocationStatus newStatus, string? reason = null)
    {
        AssertStatusKnown(newStatus);
        if (Status == newStatus) return; // no-op suppresses event

        var oldStatus = Status;
        Status = newStatus;
        RaiseEvent(new AllocationStatusChanged(Id, oldStatus, newStatus, NormalizeReason(reason), DateTimeOffset.UtcNow));
    }

    // Reassign — swap the covering resource on the SAME demand (amendment C1).
    // Preserves Id, span, rate%, status, demand.
    //
    // Boundaries are untouched, and that is the point of the implicit referent
    // (ADR-0035 §3): they follow the NODE, not the person. Swapping who covers
    // the work never moves a date. The new person's declared availability is a
    // constraint that is DISPLAYED against the block, never one that moves it.
    public void Reassign(Guid resourceId)
    {
        if (resourceId == Guid.Empty)
            throw new DomainException("Coverage must reference a resource.");
        if (ResourceId == resourceId) return; // no-op suppresses event

        var old = ResourceId;
        ResourceId = resourceId;
        RaiseEvent(new AllocationResourceChanged(Id, old, resourceId, DateTimeOffset.UtcNow));
    }

    // Retarget — re-point the coverage to ANOTHER demand (amendment C1). The
    // service passes the new demand's node so the denormalized ProjectNodeId stays
    // consistent (I8). Preserves rate%, status and resource. The boundaries keep
    // their state, but a FollowsNode edge now follows a DIFFERENT node: the
    // service re-snaps it to the new node's dates (and pins it when that node
    // has no such date), because it is the layer that can read them.
    public void RetargetToDemand(Guid newDemandId, Guid newProjectNodeId)
    {
        if (newDemandId == Guid.Empty)
            throw new DomainException("Coverage must reference a demand.");
        if (newProjectNodeId == Guid.Empty)
            throw new DomainException("Coverage must reference a project node.");
        if (DemandId == newDemandId && ProjectNodeId == newProjectNodeId) return; // no-op

        var oldDemand = DemandId;
        DemandId = newDemandId;
        ProjectNodeId = newProjectNodeId;
        RaiseEvent(new AllocationRetargeted(Id, oldDemand, newDemandId, DateTimeOffset.UtcNow));
    }

    // Called by the service layer just before repository.Remove. This is now the
    // canonical deallocation: the coverage leaves, the demand underneath persists
    // and re-surfaces as uncovered (revision §8, ADR-0012 semantics preserved).
    public void MarkDeleted() =>
        RaiseEvent(new AllocationDeleted(Id, DateTimeOffset.UtcNow));

    public const decimal MaxAllocationPercent = 1000m;

    // ── helpers ─────────────────────────────────────────────────────────────

    // The one place the span changes. Validates ordering, suppresses the no-op,
    // raises AllocationPeriodChanged. Anchor handling is the caller's business:
    // ChangePeriod pins what moved, FollowNode/FollowNodes keep the tie.
    private void SetPeriod(DateOnly newStart, DateOnly newEnd, string? reason)
    {
        if (newStart > newEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        if (PeriodStart == newStart && PeriodEnd == newEnd) return; // no-op suppresses event

        var oldStart = PeriodStart;
        var oldEnd = PeriodEnd;
        PeriodStart = newStart;
        PeriodEnd = newEnd;
        RaiseEvent(new AllocationPeriodChanged(
            Id, oldStart, oldEnd, newStart, newEnd, NormalizeReason(reason), DateTimeOffset.UtcNow));
    }

    private void SetAnchor(BoundaryEdge edge, AnchorKind anchor, string? reason)
    {
        var old = AnchorOf(edge);
        if (old == anchor) return; // no-op suppresses event

        if (edge == BoundaryEdge.Start) StartAnchor = anchor; else EndAnchor = anchor;
        RaiseEvent(new AllocationAnchorChanged(Id, edge, old, anchor, NormalizeReason(reason), DateTimeOffset.UtcNow));
    }

    private static void AssertEdgeKnown(BoundaryEdge edge)
    {
        if (!Enum.IsDefined(edge))
            throw new DomainException($"Invalid boundary edge '{edge}'.");
    }

    private static void AssertCommonInputs(
        Guid projectNodeId, DateOnly periodStart, DateOnly periodEnd, decimal allocationPercent)
    {
        if (projectNodeId == Guid.Empty)
            throw new DomainException("Coverage must reference a project node.");
        if (periodStart > periodEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        AssertPercentInRange(allocationPercent);
    }

    private static void AssertPercentInRange(decimal percent)
    {
        if (percent <= 0m || percent > MaxAllocationPercent)
            throw new DomainException(
                $"AllocationPercent must be in the range (0, {MaxAllocationPercent}].");
    }

    private static void AssertStatusKnown(AllocationStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new DomainException($"Invalid allocation status '{status}'.");
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is null) return null;
        var trimmed = notes.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeReason(string? reason)
    {
        if (reason is null) return null;
        var trimmed = reason.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
