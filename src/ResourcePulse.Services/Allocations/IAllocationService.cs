using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Allocations;

// READ side only. Plan mutation lives behind the command envelope
// (IPlanCommandService, ADR-0018). The fine-grained write methods are retired.
public interface IAllocationService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);

    // Detail read — populates ResolvedHours (when applicable; null for placeholders).
    Task<ServiceResult<AllocationReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Range-filtered list reads — ResolvedHours is null (see ADR-0013, D1).
    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    // The flat plan slice (api-roundtrip-consolidation.md P3): every coverage
    // overlapping [from, to], across all resources and projects. Range capped
    // at 366 days like the load/coverage reads.
    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetInRangeAsync(
        DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    // Sidecar — cheap per-row hours lookup at current capacity.
    // Returns Conflict if invoked on a placeholder allocation (no resource ⇒ no capacity).
    Task<ServiceResult<AllocationResolvedHoursDto>> GetResolvedHoursAsync(
        Guid id, CancellationToken ct = default);
}
