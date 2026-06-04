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

    Task<ServiceResult<Unit>> AssignTeamAsync(Guid resourceId, AssignTeamDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> AssignRoleAsync(Guid resourceId, AssignRoleDto dto, CancellationToken ct = default);

    Task<ServiceResult<ResourceSkillDto>> AddSkillAsync(Guid resourceId, AddOrUpdateResourceSkillDto dto, CancellationToken ct = default);
    Task<ServiceResult<ResourceSkillDto>> UpdateSkillLevelAsync(Guid resourceId, Guid skillId, AddOrUpdateResourceSkillDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default);

    Task<ServiceResult<ResourceSkillDto>> ApproveSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default);
    Task<ServiceResult<ResourceSkillDto>> RejectSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default);
    Task<ServiceResult<ResourceSkillDto>> ReturnSkillToPendingAsync(Guid resourceId, Guid skillId, CancellationToken ct = default);

    Task<ServiceResult<ResourceTagDto>> AddTagAsync(Guid resourceId, AddResourceTagDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveTagAsync(Guid resourceId, Guid tagId, CancellationToken ct = default);
}
