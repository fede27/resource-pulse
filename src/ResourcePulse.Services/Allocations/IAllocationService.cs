using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Allocations;

public interface IAllocationService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<AllocationReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> CreateAsync(CreateAllocationDto dto, CancellationToken ct = default);
    Task<ServiceResult<AllocationReadDto>> UpdateAsync(Guid id, UpdateAllocationDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
