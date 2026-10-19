using TimeSloth.Domain.Allocations;

namespace TimeSloth.Domain.Capacity;

// One block whose window a gesture moved: the same coverage before and after.
public readonly record struct BlockShift(Allocation Before, Allocation After);

// A run of days on which a person's committed rate% changed. Run-length over
// the days that HAVE capacity: a weekend inside the run does not split it, a
// day the gesture did not touch does.
public sealed record LoadInterval(
    DateOnly From,
    DateOnly To,
    decimal PercentBefore,
    decimal PercentAfter,
    IReadOnlyList<Guid> OtherRootIds);

// Days the gesture ADDED on which the person has no capacity because the
// company is closed. The block is valid there and resolves to zero hours; the
// utilization of those days is undefined, not 0% and not overload.
public sealed record ZeroCapacityRun(DateOnly From, DateOnly To, string Reason);

// Hours the gesture added outside the person's declared availability. They
// count in the coverage — capacity never reads availability (ADR-0035 §3).
public sealed record BeyondAvailability(DateOnly From, DateOnly To, int Days, TimeSpan Hours);

public sealed record PersonConsequence(
    TimeSpan HoursDelta,
    IReadOnlyList<LoadInterval> Intervals,
    IReadOnlyList<ZeroCapacityRun> ZeroCapacity,
    BeyondAvailability? Beyond);

// What a block-moving gesture does to the people it touches, in the two
// currencies the dates do not show: hours and committed rate%. Pure — capacity,
// closures and the blocks that count toward the band are passed IN, as with
// every calculator here (ADR-0010). It prices; it does not judge: which band a
// percentage falls in, and whether a transition is worth a row, is the caller's.
public static class PlanConsequenceCalculator
{
    // The days in exactly one of the two windows. Added = in After only.
    public static IEnumerable<(DateOnly Date, bool Added)> ChangedDays(BlockShift shift)
    {
        var (b, a) = (shift.Before, shift.After);
        for (var d = b.PeriodStart; d <= b.PeriodEnd; d = d.AddDays(1))
            if (d < a.PeriodStart || d > a.PeriodEnd) yield return (d, false);
        for (var d = a.PeriodStart; d <= a.PeriodEnd; d = d.AddDays(1))
            if (d < b.PeriodStart || d > b.PeriodEnd) yield return (d, true);
    }

    // Signed hours the shift adds to (or takes from) its person: % × capacity
    // over the changed days, the same arithmetic as the reconciliation.
    public static TimeSpan HoursDelta(BlockShift shift, Func<DateOnly, TimeSpan> capacityOn)
    {
        var delta = TimeSpan.Zero;
        foreach (var (date, added) in ChangedDays(shift))
        {
            var hours = LoadCalculator.HoursFor(capacityOn(date), shift.After.AllocationPercent);
            delta += added ? hours : -hours;
        }
        return delta;
    }

    // `shifts` are one person's moved blocks. `loadBefore` / `loadAfter` are the
    // blocks that count toward that person's band — every project, already
    // narrowed by whatever status rule the caller applies (the calculators stay
    // status-agnostic). `gestureRootIds` are left out of OtherRootIds so an
    // interval names only the projects the planner is NOT looking at.
    public static PersonConsequence ForPerson(
        IReadOnlyCollection<BlockShift> shifts,
        IReadOnlyCollection<Allocation> loadBefore,
        IReadOnlyCollection<Allocation> loadAfter,
        Func<DateOnly, TimeSpan> capacityOn,
        IReadOnlyDictionary<Guid, Guid> rootByNodeId,
        IReadOnlySet<Guid> gestureRootIds,
        IReadOnlyCollection<(DateOnly From, DateOnly To, string Reason)> closures,
        DateOnly? availableFrom,
        DateOnly? availableUntil)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(capacityOn);

        var hoursDelta = TimeSpan.Zero;
        var changed = new HashSet<DateOnly>();
        var added = new SortedSet<DateOnly>();
        var beyondDays = new SortedSet<DateOnly>();
        var beyondHours = TimeSpan.Zero;

        foreach (var shift in shifts)
        {
            hoursDelta += HoursDelta(shift, capacityOn);
            foreach (var (date, isAdded) in ChangedDays(shift))
            {
                changed.Add(date);
                if (!isAdded) continue;
                added.Add(date);

                var outside = (availableFrom is { } af && date < af) || (availableUntil is { } au && date > au);
                var capacity = capacityOn(date);
                if (!outside || capacity == TimeSpan.Zero) continue;
                beyondDays.Add(date);
                beyondHours += LoadCalculator.HoursFor(capacity, shift.After.AllocationPercent);
            }
        }

        return new PersonConsequence(
            hoursDelta,
            LoadIntervals(changed, loadBefore, loadAfter, capacityOn, rootByNodeId, gestureRootIds),
            ZeroCapacityRuns(added, capacityOn, closures),
            beyondDays.Count == 0
                ? null
                : new BeyondAvailability(beyondDays.Min, beyondDays.Max, beyondDays.Count, beyondHours));
    }

    private static List<LoadInterval> LoadIntervals(
        HashSet<DateOnly> changed,
        IReadOnlyCollection<Allocation> loadBefore,
        IReadOnlyCollection<Allocation> loadAfter,
        Func<DateOnly, TimeSpan> capacityOn,
        IReadOnlyDictionary<Guid, Guid> rootByNodeId,
        IReadOnlySet<Guid> gestureRootIds)
    {
        var intervals = new List<LoadInterval>();
        if (changed.Count == 0) return intervals;

        (DateOnly From, DateOnly To, decimal Before, decimal After, HashSet<Guid> Others)? run = null;
        void Close()
        {
            if (run is { } r && r.Before != r.After)
                intervals.Add(new LoadInterval(r.From, r.To, r.Before, r.After, r.Others.OrderBy(id => id).ToList()));
            run = null;
        }

        for (var d = changed.Min(); d <= changed.Max(); d = d.AddDays(1))
        {
            if (!changed.Contains(d)) { Close(); continue; }
            // A day without capacity has no utilization to compare: it neither
            // extends the run nor breaks it.
            if (capacityOn(d) == TimeSpan.Zero) continue;

            var before = PercentOn(loadBefore, d);
            var after = PercentOn(loadAfter, d);
            if (run is { } r && r.Before == before && r.After == after)
            {
                AddOtherRoots(r.Others, loadAfter, d, rootByNodeId, gestureRootIds);
                run = r with { To = d };
                continue;
            }

            Close();
            var others = new HashSet<Guid>();
            AddOtherRoots(others, loadAfter, d, rootByNodeId, gestureRootIds);
            run = (d, d, before, after, others);
        }
        Close();
        return intervals;
    }

    private static decimal PercentOn(IReadOnlyCollection<Allocation> blocks, DateOnly date)
    {
        var total = 0m;
        foreach (var a in blocks)
            if (a.PeriodStart <= date && date <= a.PeriodEnd) total += a.AllocationPercent;
        return total;
    }

    private static void AddOtherRoots(
        HashSet<Guid> others,
        IReadOnlyCollection<Allocation> blocks,
        DateOnly date,
        IReadOnlyDictionary<Guid, Guid> rootByNodeId,
        IReadOnlySet<Guid> gestureRootIds)
    {
        foreach (var a in blocks)
        {
            if (date < a.PeriodStart || date > a.PeriodEnd) continue;
            var root = rootByNodeId.TryGetValue(a.ProjectNodeId, out var r) ? r : a.ProjectNodeId;
            if (!gestureRootIds.Contains(root)) others.Add(root);
        }
    }

    private static List<ZeroCapacityRun> ZeroCapacityRuns(
        SortedSet<DateOnly> added,
        Func<DateOnly, TimeSpan> capacityOn,
        IReadOnlyCollection<(DateOnly From, DateOnly To, string Reason)> closures)
    {
        var runs = new List<ZeroCapacityRun>();
        foreach (var d in added)
        {
            if (capacityOn(d) != TimeSpan.Zero) continue;
            // Only a closure is named. A weekend is zero capacity too, and
            // nobody needs telling.
            var closure = closures.FirstOrDefault(c => c.From <= d && d <= c.To);
            if (closure.Reason is null) continue;

            if (runs.Count > 0 && runs[^1].Reason == closure.Reason && runs[^1].To.AddDays(1) == d)
                runs[^1] = runs[^1] with { To = d };
            else
                runs.Add(new ZeroCapacityRun(d, d, closure.Reason));
        }
        return runs;
    }
}
