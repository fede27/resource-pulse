namespace ResourcePulse.Domain.Capacity;

// One row per date in the project-node-load output. Per-node = domanda
// (assigned + placeholder), per ADR-0016 §5.
//
//   TotalHours       — sum of hours from assigned allocations (resource set);
//                      requires per-resource capacity for the conversion.
//   ByResource       — per-resource breakdown of TotalHours.
//   PlaceholderRatePercent — sum of rate% from placeholder allocations on the
//                      date. Carried in % because a placeholder has no resource
//                      and therefore no resolved hours: the rate is the only
//                      magnitude that survives without a capacity dictionary.
public readonly record struct DailyNodeLoad(
    DateOnly Date,
    TimeSpan TotalHours,
    IReadOnlyDictionary<Guid, TimeSpan> ByResource,
    decimal PlaceholderRatePercent);
