using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Projects;

public interface IProjectNodeService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<ProjectNodeReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ProjectNodeReadDto>>> GetSubtreeAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<ProjectNodeReadDto>> CreateAsync(CreateProjectNodeDto dto, CancellationToken ct = default);
    Task<ServiceResult<ProjectNodeReadDto>> UpdateAsync(Guid id, UpdateProjectNodeDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<Unit>> ReparentAsync(Guid id, ReparentDto dto, CancellationToken ct = default);

    // Capacity-planning artifacts — work on Project + Phase nodes (domain enforces).
    Task<ServiceResult<Unit>> BaselineAsync(Guid id, BaselineDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RebaselineAsync(Guid id, RebaselineDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> ReplanAsync(Guid id, ReplanDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> BackfillActualsAsync(Guid id, BackfillActualsDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RecalculatePlannedFromChildrenAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RecalculateBaselineFromChildrenAsync(Guid id, CancellationToken ct = default);

    // Tags — allowed at any node level.
    Task<ServiceResult<ProjectNodeTagDto>> AddTagAsync(Guid id, AddProjectNodeTagDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct = default);

    // ── Root-only operations ────────────────────────────────────────────────
    Task<ServiceResult<ProjectNodeReadDto>> UpdateProjectAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);

    Task<ServiceResult<Unit>> StartAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<Unit>> CompleteAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<Unit>> SuspendAsync(Guid id, ReasonDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> ResumeAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<Unit>> CancelAsync(Guid id, ReasonDto dto, CancellationToken ct = default);

    Task<ServiceResult<ProjectSkillRequirementDto>> AddSkillRequirementAsync(Guid id, AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct = default);
    Task<ServiceResult<ProjectSkillRequirementDto>> UpdateSkillRequirementLevelAsync(Guid id, Guid skillId, AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveSkillRequirementAsync(Guid id, Guid skillId, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<ProjectNodeReadDto>>> GetProjectsActiveInRangeAsync(DateOnly from, DateOnly to, DateSource dateSource, CancellationToken ct = default);
}
