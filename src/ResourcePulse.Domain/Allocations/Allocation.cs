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
// Local invariants only:
//   - period start <= period end
//   - rate% in (0, 1000] — typo safeguard, not a cap (ADR-0013)
//   - demand, node and resource ids non-empty
//
// Cross-aggregate (I3, I4, I6, I8) live in the service layer (PlanCommandService).
// Overlap on the same (resource, project_node) sums (ADR-0014) — never re-checked.
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

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Allocation() { }

    // Creates a coverage. Default Status = Tentative (ADR-0015 §1). projectNodeId
    // is passed by the service from the target demand (denormalization, I8).
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
    public void ChangePeriod(DateOnly newStart, DateOnly newEnd, string? reason = null)
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

    // ── Span operations (ADR-0017) ───────────────────────────────────────────

    // SplitAt — structural, NON-destructive. Cuts the span at `date` into two
    // adjacent, NON-overlapping blocks: this becomes [PeriodStart, date-1], the
    // returned sibling covers [date, PeriodEnd]. Both carry the same rate%, status,
    // demand, node and resource. `date` must be strictly interior.
    public Allocation SplitAt(DateOnly date, string? reason = null)
    {
        if (date <= PeriodStart || date > PeriodEnd)
            throw new DomainException(
                "Split date must be strictly inside the allocation span (PeriodStart < date <= PeriodEnd).");

        var second = CloneForSplit(date, PeriodEnd);
        PeriodEnd = date.AddDays(-1);

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
    private Allocation CloneForSplit(DateOnly start, DateOnly end) =>
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
            Notes = Notes
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
    // Preserves Id, span, rate%, status, demand. (Was placeholder→assigned.)
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
    // consistent (I8). Preserves span, rate%, status and resource.
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
