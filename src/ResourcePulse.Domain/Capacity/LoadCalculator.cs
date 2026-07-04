using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;

namespace ResourcePulse.Domain.Capacity;

// Pure load math. No I/O, no clock dependency, no EF references. Sibling of
// CapacityCalculator — load is a function of capacity. The two compose at the
// service layer (LiveLoadQueryService consumes ICapacityQueryService).
//
// Algorithm for (Resource R, DateOnly D):
//   hours = sum over { a in allocations : a.ResourceId == R, D ∈ [a.PeriodStart, a.PeriodEnd] }
//           of capacityForDate * (a.AllocationPercent / 100)
//
// The calculator trusts its inputs:
//   - allocations must reference valid project nodes (caller's responsibility)
//   - capacity dictionaries must cover the requested range (missing dates default to TimeSpan.Zero)
//   - no activeness checks — that is a service-layer invariant
//
// Overlapping allocations on the same (resource, project_node) are first-class
// and their rate% sums per ADR-0014. The summation loop already handles this:
// every active allocation contributes; there is no special case for same-node
// overlap. The same rule applies cross-project (ADR-0011, I5).
//
// Coverage model (Phase 5.1, ADR-0025): every Allocation is a coverage with a
// real resource. The old placeholder split is gone — uncovered work is now a
// Demand with no coverage, computed by the demand-coverage read model (Phase 5.2),
// not by a placeholder branch here. Per-node load is therefore coverage hours
// only.
//
// Determinism: allocations iterated in OrderBy(Id). Current logic is a sum so
// order doesn't change the result, but the contract is documented for callers
// that may compute hash-based aggregates over the per-allocation contributions.
public static class LoadCalculator
{
    public static TimeSpan ForResourceAndDate(
        Guid resourceId,
        IReadOnlyCollection<Allocation> allocations,
        TimeSpan capacityForDate,
        DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        var hours = TimeSpan.Zero;
        foreach (var a in allocations.OrderBy(a => a.Id))
        {
            if (a.ResourceId != resourceId) continue; // other resources
            if (date < a.PeriodStart || date > a.PeriodEnd) continue;
            hours += HoursFor(capacityForDate, a.AllocationPercent);
        }
        return hours;
    }

    public static IEnumerable<DailyLoad> ForResourceAndRange(
        Guid resourceId,
        IReadOnlyCollection<Allocation> allocations,
        IReadOnlyDictionary<DateOnly, TimeSpan> capacityByDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(capacityByDate);

        if (from > toInclusive)
            yield break;

        // Pre-filter once; per-date loop only walks this resource's coverage.
        var ordered = allocations
            .Where(a => a.ResourceId == resourceId)
            .OrderBy(a => a.Id)
            .ToList();

        for (var date = from; date <= toInclusive; date = date.AddDays(1))
        {
            var capacity = capacityByDate.TryGetValue(date, out var c) ? c : TimeSpan.Zero;
            var (hours, anyActive) = HoursAndPresence(ordered, capacity, date);
            yield return new DailyLoad(date, hours, LoadPercent(hours, capacity, anyActive));
        }
    }

    private static (TimeSpan hours, bool anyActive) HoursAndPresence(
        IReadOnlyList<Allocation> resourceAllocations, TimeSpan capacity, DateOnly date)
    {
        var hours = TimeSpan.Zero;
        var any = false;
        foreach (var a in resourceAllocations)
        {
            if (date < a.PeriodStart || date > a.PeriodEnd) continue;
            any = true;
            hours += HoursFor(capacity, a.AllocationPercent);
        }
        return (hours, any);
    }

    // Exact-node load: only blocks whose ProjectNodeId == projectNodeId.
    public static IEnumerable<DailyNodeLoad> ForProjectNodeAndRange(
        Guid projectNodeId,
        IReadOnlyCollection<Allocation> allocations,
        IReadOnlyDictionary<(Guid ResourceId, DateOnly Date), TimeSpan> capacityByResourceAndDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(capacityByResourceAndDate);

        // Pre-filter once; the per-date loop only needs allocations on this node.
        return AggregateNodeLoad(
            allocations.Where(a => a.ProjectNodeId == projectNodeId),
            capacityByResourceAndDate, from, toInclusive);
    }

    // Subtree load (ADR-0022): the caller passes the allocations of the whole
    // subtree (root + descendants, scoped via Path prefix at the service layer);
    // the calculator sums them ALL, regardless of which node each block sits on.
    // Invariant I1 only permits allocations on Project/Phase nodes, so a subtree
    // can carry blocks at the root AND at intermediate Phase nodes — this is the
    // aggregate a "whole project" view needs. No node-id filter here: the scoping
    // already happened in the query.
    public static IEnumerable<DailyNodeLoad> ForProjectSubtreeAndRange(
        IReadOnlyCollection<Allocation> subtreeAllocations,
        IReadOnlyDictionary<(Guid ResourceId, DateOnly Date), TimeSpan> capacityByResourceAndDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(subtreeAllocations);
        ArgumentNullException.ThrowIfNull(capacityByResourceAndDate);

        return AggregateNodeLoad(subtreeAllocations, capacityByResourceAndDate, from, toInclusive);
    }

    // Shared per-date aggregation. `scopedAllocations` is already scoped by the
    // caller (exact node or whole subtree); this method does not filter by node.
    private static IEnumerable<DailyNodeLoad> AggregateNodeLoad(
        IEnumerable<Allocation> scopedAllocations,
        IReadOnlyDictionary<(Guid ResourceId, DateOnly Date), TimeSpan> capacityByResourceAndDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        if (from > toInclusive)
            yield break;

        var nodeAllocations = scopedAllocations
            .OrderBy(a => a.Id)
            .ToList();

        for (var date = from; date <= toInclusive; date = date.AddDays(1))
        {
            var byResource = new Dictionary<Guid, TimeSpan>();
            var total = TimeSpan.Zero;

            foreach (var a in nodeAllocations)
            {
                if (date < a.PeriodStart || date > a.PeriodEnd) continue;

                var resourceId = a.ResourceId;
                var capacity = capacityByResourceAndDate.TryGetValue((resourceId, date), out var c)
                    ? c
                    : TimeSpan.Zero;

                var hours = HoursFor(capacity, a.AllocationPercent);
                if (hours == TimeSpan.Zero) continue;

                byResource.TryGetValue(resourceId, out var existing);
                byResource[resourceId] = existing + hours;
                total += hours;
            }

            yield return new DailyNodeLoad(date, total, byResource);
        }
    }

    // Resource commitment profile (gap #4+#10): a run-length-encoded view of the
    // resource's committed rate% over [from, toInclusive], decomposed by ROOT
    // project. Capacity-independent — the percent is the sum of active assigned
    // rate% (placeholders carry no ResourceId, so they are naturally excluded),
    // not a capacity-normalised figure. This keeps the read-model cheap (no
    // per-resource capacity series) and the per-project decomposition exact (the
    // shares sum to the segment percent). See ADR-0023.
    //
    // `projectRootByNodeId` maps each allocation's ProjectNodeId to the id of its
    // root project node (the service resolves this from the materialized Path).
    // A node missing from the map falls back to grouping under its own id.
    public static IReadOnlyList<LoadSegment> ResourceCommitmentProfile(
        Guid resourceId,
        IReadOnlyCollection<Allocation> allocations,
        IReadOnlyDictionary<Guid, Guid> projectRootByNodeId,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(projectRootByNodeId);

        var segments = new List<LoadSegment>();
        if (from > toInclusive)
            return segments;

        var ordered = allocations
            .Where(a => a.ResourceId == resourceId)
            .OrderBy(a => a.Id)
            .ToList();

        DateOnly runStart = from;
        var (runPercent, runByProject) = DayCommitment(ordered, projectRootByNodeId, from);

        for (var date = from.AddDays(1); date <= toInclusive; date = date.AddDays(1))
        {
            var (percent, byProject) = DayCommitment(ordered, projectRootByNodeId, date);
            if (percent == runPercent && SameShares(runByProject, byProject))
                continue; // extend the current run

            segments.Add(new LoadSegment(runStart, date.AddDays(-1), runPercent, runByProject));
            runStart = date;
            runPercent = percent;
            runByProject = byProject;
        }

        segments.Add(new LoadSegment(runStart, toInclusive, runPercent, runByProject));
        return segments;
    }

    // Demand-coverage reconciliation (Phase 5.2, ADR-0025/0026): for each demand,
    // sum the resolved hours (% × capacity) of the coverage on it within
    // [from, toInclusive], and compare to the demand's target. Pure — capacity is
    // passed in (per-resource-per-date), never loaded here.
    //
    //   CoveredHours = Σ over coverage a where a.DemandId == demand.Id, over dates
    //                  d in [max(from, a.Start), min(to, a.End)], of
    //                  capacity(a.Resource, d) × a.Percent/100.
    //   GapHours     = RequiredHours − CoveredHours, or null when RequiredHours is
    //                  null (best-effort). Negative gap = surplus, surfaced.
    //
    // A demand with no coverage yields CoveredHours = 0 (gap = full target, or null
    // if best-effort). Scalar (Decision 4): no per-bucket distribution.
    public static IReadOnlyList<DemandCoverage> CoverageForDemands(
        IReadOnlyCollection<Demand> demands,
        IReadOnlyCollection<Allocation> coverage,
        IReadOnlyDictionary<(Guid ResourceId, DateOnly Date), TimeSpan> capacityByResourceAndDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(demands);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(capacityByResourceAndDate);

        // Group coverage by demand once.
        var byDemand = coverage
            .GroupBy(a => a.DemandId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Id).ToList());

        var result = new List<DemandCoverage>(demands.Count);
        foreach (var d in demands.OrderBy(x => x.Id))
        {
            var covered = TimeSpan.Zero;
            if (byDemand.TryGetValue(d.Id, out var blocks) && from <= toInclusive)
            {
                foreach (var a in blocks)
                {
                    var start = a.PeriodStart > from ? a.PeriodStart : from;
                    var end = a.PeriodEnd < toInclusive ? a.PeriodEnd : toInclusive;
                    for (var date = start; date <= end; date = date.AddDays(1))
                    {
                        var cap = capacityByResourceAndDate.TryGetValue((a.ResourceId, date), out var c)
                            ? c : TimeSpan.Zero;
                        covered += HoursFor(cap, a.AllocationPercent);
                    }
                }
            }

            TimeSpan? gap = d.RequiredHours is TimeSpan req ? req - covered : null;
            result.Add(new DemandCoverage(d.Id, d.ProjectNodeId, d.RoleId, d.RequiredHours, covered, gap));
        }
        return result;
    }

    // Committed rate% on a single date, total and decomposed by root project.
    private static (decimal Percent, Dictionary<Guid, decimal> ByProject) DayCommitment(
        IReadOnlyList<Allocation> resourceAllocations,
        IReadOnlyDictionary<Guid, Guid> projectRootByNodeId,
        DateOnly date)
    {
        var total = 0m;
        var byProject = new Dictionary<Guid, decimal>();
        foreach (var a in resourceAllocations)
        {
            if (date < a.PeriodStart || date > a.PeriodEnd) continue;
            var root = projectRootByNodeId.TryGetValue(a.ProjectNodeId, out var r) ? r : a.ProjectNodeId;
            byProject.TryGetValue(root, out var existing);
            byProject[root] = existing + a.AllocationPercent;
            total += a.AllocationPercent;
        }
        return (total, byProject);
    }

    private static bool SameShares(IReadOnlyDictionary<Guid, decimal> a, IReadOnlyDictionary<Guid, decimal> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (k, v) in a)
            if (!b.TryGetValue(k, out var bv) || bv != v) return false;
        return true;
    }

    // capacity * (percent / 100), carried in decimal ticks to avoid double drift.
    private static TimeSpan HoursFor(TimeSpan capacity, decimal percent)
    {
        if (capacity == TimeSpan.Zero) return TimeSpan.Zero;
        var ticks = (long)(capacity.Ticks * percent / 100m);
        return TimeSpan.FromTicks(ticks);
    }

    // no allocation active   -> 0
    // capacity == 0 (with active allocation) -> decimal.MaxValue sentinel
    // otherwise              -> (hours / capacity) * 100
    private static decimal LoadPercent(TimeSpan hours, TimeSpan capacity, bool anyActive)
    {
        if (!anyActive) return 0m;
        if (capacity == TimeSpan.Zero) return decimal.MaxValue;
        return (decimal)hours.Ticks / capacity.Ticks * 100m;
    }
}
