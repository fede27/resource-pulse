using ResourcePulse.Domain.Allocations;

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
//   - no overlap or activeness checks — those are service-layer invariants
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
            if (a.ResourceId != resourceId) continue;
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

        // Pre-filter once; per-date loop only walks this resource's allocations.
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

    public static IEnumerable<DailyNodeLoad> ForProjectNodeAndRange(
        Guid projectNodeId,
        IReadOnlyCollection<Allocation> allocations,
        IReadOnlyDictionary<(Guid ResourceId, DateOnly Date), TimeSpan> capacityByResourceAndDate,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(capacityByResourceAndDate);

        if (from > toInclusive)
            yield break;

        // Pre-filter once; the per-date loop only needs allocations on this node.
        var nodeAllocations = allocations
            .Where(a => a.ProjectNodeId == projectNodeId)
            .OrderBy(a => a.Id)
            .ToList();

        for (var date = from; date <= toInclusive; date = date.AddDays(1))
        {
            var byResource = new Dictionary<Guid, TimeSpan>();
            var total = TimeSpan.Zero;

            foreach (var a in nodeAllocations)
            {
                if (date < a.PeriodStart || date > a.PeriodEnd) continue;

                var capacity = capacityByResourceAndDate.TryGetValue((a.ResourceId, date), out var c)
                    ? c
                    : TimeSpan.Zero;

                var hours = HoursFor(capacity, a.AllocationPercent);
                if (hours == TimeSpan.Zero) continue;

                byResource.TryGetValue(a.ResourceId, out var existing);
                byResource[a.ResourceId] = existing + hours;
                total += hours;
            }

            yield return new DailyNodeLoad(date, total, byResource);
        }
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
