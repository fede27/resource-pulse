namespace ResourcePulse.Domain.Configuration;

// Aggregation grain for bucketed views (ADR-0020). The set {day, week, month} is
// a fixed enum — CONSTANT, not config (no quarter: out of target). Week/month
// alignment (calendar vs fiscal) is deferred and will derive from the
// BusinessCalendar, not a knob of this layer.
public enum BucketGrain
{
    Day = 1,
    Week = 2,
    Month = 3
}
