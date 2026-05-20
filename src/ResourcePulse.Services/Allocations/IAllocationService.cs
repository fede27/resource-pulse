using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Allocations;

public interface IAllocationService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);

    // Detail read — populates ResolvedHours.
    Task<ServiceResult<AllocationReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Range-filtered list reads — ResolvedHours is null (see ADR-0013, D1).
    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    // Sidecar — cheap per-row hours lookup at current capacity.
    Task<ServiceResult<AllocationResolvedHoursDto>> GetResolvedHoursAsync(
        Guid id, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> CreateByPercentAsync(
        CreateByPercentDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> CreateByHoursAsync(
        CreateByHoursDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> UpdateAsync(
        Guid id, UpdateAllocationDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> MoveAsync(
        Guid id, MoveAllocationDto dto, CancellationToken ct = default);

    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
