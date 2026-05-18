namespace ResourcePulse.Domain.Capacity;

// One row per date in the project-node-load output. ByResource breaks total down
// per resource so callers don't need to query allocations again to attribute load.
public readonly record struct DailyNodeLoad(
    DateOnly Date,
    TimeSpan TotalHours,
    IReadOnlyDictionary<Guid, TimeSpan> ByResource);
