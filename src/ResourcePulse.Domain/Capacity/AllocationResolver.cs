using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Capacity;

// Pure conversion between two vocabularies the user thinks in:
//   - "I want this resource at X% on this project"  (rate-shaped)
//   - "I want this resource to spend Y hours on this project over the window" (quantity-shaped)
//
// Persistence is always in percent (see ADR-0007). This resolver is the only
// place hours <-> percent conversions happen. Callers compute the window's
// total capacity once and pass it in; the resolver has no I/O.
//
// Rounding: results are quantized to 2 decimals (matches the storage column
// numeric(6,2)) using banker's rounding (ToEven) so repeated round-trips do
// not systematically drift upward.
public static class AllocationResolver
{
    private const int PercentScale = 2;

    public static decimal PercentForHours(TimeSpan targetHours, TimeSpan capacityInWindow)
    {
        if (targetHours <= TimeSpan.Zero)
            throw new DomainException("targetHours must be greater than zero.");
        if (capacityInWindow <= TimeSpan.Zero)
            throw new DomainException(
                "Cannot resolve a percent against zero capacity. Resolve the calendar first.");

        // decimal arithmetic — TimeSpan.Ticks is long, division is exact in decimal.
        var raw = (decimal)targetHours.Ticks / capacityInWindow.Ticks * 100m;
        return decimal.Round(raw, PercentScale, MidpointRounding.ToEven);
    }

    public static TimeSpan HoursForPercent(decimal percent, TimeSpan capacityInWindow)
    {
        if (percent <= 0m)
            throw new DomainException("percent must be greater than zero.");
        // capacityInWindow == zero is legal here — yields TimeSpan.Zero hours,
        // which is the truthful answer ("the percent buys you no hours because
        // there's no capacity"). Callers that want to reject zero capacity
        // (e.g. CreateByHours, MoveAsync KeepHours) gate it themselves before
        // calling.

        var fraction = percent / 100m;
        var ticks = (long)((decimal)capacityInWindow.Ticks * fraction);
        return TimeSpan.FromTicks(ticks);
    }
}
