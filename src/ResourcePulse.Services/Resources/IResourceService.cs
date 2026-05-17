using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Resources;

public interface IResourceService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<ResourceReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<ResourceReadDto>> CreateAsync(CreateResourceDto dto, CancellationToken ct = default);
    Task<ServiceResult<ResourceReadDto>> UpdateAsync(Guid id, UpdateResourceDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<WorkWindowDto>> AddWorkWindowAsync(Guid resourceId, WorkWindowDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveWorkWindowAsync(Guid resourceId, Guid windowId, CancellationToken ct = default);
    Task<ServiceResult<IndividualAdjustmentDto>> AddAdjustmentAsync(Guid resourceId, IndividualAdjustmentDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveAdjustmentAsync(Guid resourceId, Guid adjustmentId, CancellationToken ct = default);
}
