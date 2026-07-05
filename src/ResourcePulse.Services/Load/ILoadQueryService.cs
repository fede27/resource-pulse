using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Demands;

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
    // `status` optionally narrows the profile to blocks with that commitment
    // status (e.g. Hard-only for the sustainability verdict); null = all blocks.
    Task<ServiceResult<IReadOnlyList<LoadSegmentDto>>> GetCommitmentProfileForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        AllocationStatus? status = null,
        CancellationToken ct = default);

    // Demand-vs-coverage reconciliation (Phase 5.2, ADR-0025/0026). Over the
    // node's subtree (node + descendants via Path prefix, ADR-0022): per demand,
    // required/covered/gap hours. Best-effort demands carry a null gap (§7).
    Task<ServiceResult<IReadOnlyList<DemandCoverageDto>>> GetDemandCoverageForProjectNodeAsync(
        Guid projectNodeId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    // Coverage of a single demand over the range.
    Task<ServiceResult<DemandCoverageDto>> GetDemandCoverageForDemandAsync(
        Guid demandId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);
}
