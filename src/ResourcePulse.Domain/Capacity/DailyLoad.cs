namespace ResourcePulse.Domain.Capacity;

// One row per date in the resource-load output. LoadPercent uses decimal.MaxValue as
// a sentinel when load exists on a date with zero capacity (full overload signal —
// Phase 5 consumes this).
public readonly record struct DailyLoad(DateOnly Date, TimeSpan Hours, decimal LoadPercent);
