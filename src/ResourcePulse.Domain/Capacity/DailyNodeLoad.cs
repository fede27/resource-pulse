namespace ResourcePulse.Domain.Capacity;

// One row per date in the project-node-load output. Per-node load is now COVERAGE
// hours only (Phase 5.1, ADR-0025): every allocation is a coverage with a real
// resource. Uncovered demand is no longer a placeholder contribution here — it is
// computed by the demand-coverage read model (Phase 5.2).
//
//   TotalHours  — sum of coverage hours on the date; needs per-resource capacity.
//   ByResource  — per-resource breakdown of TotalHours.
public readonly record struct DailyNodeLoad(
    DateOnly Date,
    TimeSpan TotalHours,
    IReadOnlyDictionary<Guid, TimeSpan> ByResource);
