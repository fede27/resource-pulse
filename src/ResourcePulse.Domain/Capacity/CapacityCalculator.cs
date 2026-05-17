using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Domain.Capacity;

// Pure capacity math. No I/O, no clock dependency, no EF references.
// Caller is responsible for passing the BusinessCalendar matching resource.BusinessCalendarId
// AND pre-filtered collections relevant to the requested date(s). The calculator trusts
// its inputs and does not re-filter or verify FKs — re-filtering would be wasted work.
//
// Composition for (Resource R, DateOnly D):
//   1. Pattern: R.WorkWindows if non-empty, else calendar.WorkWindows. Sum durations of
//      windows applicable to D (matching DayOfWeek + validity covers D).
//   2. Closures: if any closure covers D, base becomes TimeSpan.Zero (hard override).
//   3. Adjustments (sum-then-clamp): absences subtract from base (full-day absence zeros
//      it), extras add. result = max(0, base - Σ absences + Σ extras). Order-independent.
public static class CapacityCalculator
{
    public static TimeSpan ForDate(
        Resource resource,
        BusinessCalendar calendar,
        IReadOnlyCollection<CompanyClosure> closures,
        DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(closures);

        var pattern = resource.WorkWindows.Count > 0
            ? resource.WorkWindows
            : calendar.WorkWindows;

        var basePattern = pattern
            .Where(w => w.AppliesTo(date))
            .Aggregate(TimeSpan.Zero, (acc, w) => acc + w.Duration);

        var isClosed = closures.Any(c => c.Covers(date));
        if (isClosed)
            basePattern = TimeSpan.Zero;

        var absenceTotal = TimeSpan.Zero;
        var extraTotal = TimeSpan.Zero;
        foreach (var adj in resource.Adjustments.OrderBy(a => a.Id))
        {
            if (!adj.Covers(date)) continue;
            switch (adj.Type)
            {
                case AdjustmentType.Absence:
                    absenceTotal += adj.Hours ?? basePattern;
                    break;
                case AdjustmentType.ExtraTime:
                    extraTotal += adj.Hours!.Value;
                    break;
            }
        }

        var result = basePattern - absenceTotal + extraTotal;
        return result < TimeSpan.Zero ? TimeSpan.Zero : result;
    }

    public static IEnumerable<DailyCapacity> ForRange(
        Resource resource,
        BusinessCalendar calendar,
        IReadOnlyCollection<CompanyClosure> closures,
        DateOnly from,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(closures);

        if (from > toInclusive)
            yield break;

        for (var date = from; date <= toInclusive; date = date.AddDays(1))
            yield return new DailyCapacity(date, ForDate(resource, calendar, closures, date));
    }
}
