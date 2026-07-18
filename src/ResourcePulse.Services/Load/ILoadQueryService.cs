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
    // Capacity-based daily load series. `status` optionally narrows to blocks
    // with that commitment status (e.g. Hard-only for band colouring — the
    // Allocazioni heatmap counts committed blocks by default and adds tentative
    // on request); null = all blocks. Twin of the load-profile filter.
    Task<ServiceResult<IReadOnlyList<DailyLoadDto>>> GetForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        AllocationStatus? status = null,
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

    // Batch twin of GetCommitmentProfileForResourceAsync (consolidation P2): the
    // profiles of a whole population in one round of queries — the profile is
    // capacity-independent, so the cost is flat in the population size.
    // `resourceIds` null/empty = all active resources; explicit ids are honoured
    // regardless of IsActive; unknown ids are simply absent from the result.
    Task<ServiceResult<IReadOnlyList<ResourceLoadProfileDto>>> GetCommitmentProfilesForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds,
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

    // Cross-project reconciliation (consolidation P4): every demand whose root
    // project is not Closed/Cancelled (I4), reconciled over the range in one
    // call — the batch twin of the per-node read; consumers pivot by the
    // DTO-resolved RootProjectId. GetOpenDemandsAsync is its filtered view.
    Task<ServiceResult<IReadOnlyList<DemandCoverageDto>>> GetDemandCoverageInRangeAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    // Coverage of a single demand over the range.
    Task<ServiceResult<DemandCoverageDto>> GetDemandCoverageForDemandAsync(
        Guid demandId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    // Open demands across the whole plan (ADR-0027): demands whose reconciliation
    // over [from, toInclusive] leaves GapHours > 0, plus best-effort demands (no
    // target ⇒ they can always absorb coverage). Excludes demands whose root
    // project is Closed/Cancelled (I4 forbids covering them anyway). `roleId`
    // optionally narrows to demands asking for that role.
    Task<ServiceResult<IReadOnlyList<OpenDemandDto>>> GetOpenDemandsAsync(
        Guid? roleId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);
}
