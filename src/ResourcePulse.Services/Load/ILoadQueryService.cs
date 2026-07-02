using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Load;

// Abstraction so future implementations (e.g. SnapshotLoadQueryService reading from a
// pre-computed daily_load_snapshots table) can be drop-in replacements for
// LiveLoadQueryService. Composes with ICapacityQueryService — load is a function
// of capacity. See ADR-0010.
public interface ILoadQueryService
{
    Task<ServiceResult<IReadOnlyList<DailyLoadDto>>> GetForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<DailyNodeLoadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    // Resource commitment profile (gap #4+#10 / ADR-0023): run-length segments of
    // the resource's committed rate% over [from, toInclusive], decomposed by root
    // project. Capacity-independent — no per-resource capacity series is loaded.
    Task<ServiceResult<IReadOnlyList<LoadSegmentDto>>> GetCommitmentProfileForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);
}
