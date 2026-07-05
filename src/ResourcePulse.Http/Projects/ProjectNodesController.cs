using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Http.Projects;

// Flat CRUD + generic operations on any ProjectNode. Operations restricted by the
// domain to specific node types (state transitions, skill requirements) live on
// ProjectsController instead.
[Route("api/project-nodes")]
public sealed class ProjectNodesController(IProjectNodeService service)
    : CrudController<CreateProjectNodeDto, UpdateProjectNodeDto, ProjectNodeReadDto, Guid>
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<ProjectNodeReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpGet("{id}/subtree")]
    [ProducesResponseType<IReadOnlyList<ProjectNodeReadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubtreeAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetSubtreeAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateProjectNodeDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateProjectNodeDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id}/reparent")]
    public async Task<IActionResult> ReparentAsync(Guid id, [FromBody] ReparentDto dto, CancellationToken ct) =>
        FromResult(await service.ReparentAsync(id, dto, ct));

    [HttpPost("{id}/baseline")]
    public async Task<IActionResult> BaselineAsync(Guid id, [FromBody] BaselineDto dto, CancellationToken ct) =>
        FromResult(await service.BaselineAsync(id, dto, ct));

    [HttpPost("{id}/rebaseline")]
    public async Task<IActionResult> RebaselineAsync(Guid id, [FromBody] RebaselineDto dto, CancellationToken ct) =>
        FromResult(await service.RebaselineAsync(id, dto, ct));

    [HttpPost("{id}/replan")]
    public async Task<IActionResult> ReplanAsync(Guid id, [FromBody] ReplanDto dto, CancellationToken ct) =>
        FromResult(await service.ReplanAsync(id, dto, ct));

    [HttpPost("{id}/backfill-actuals")]
    public async Task<IActionResult> BackfillActualsAsync(Guid id, [FromBody] BackfillActualsDto dto, CancellationToken ct) =>
        FromResult(await service.BackfillActualsAsync(id, dto, ct));

    [HttpPost("{id}/recalculate-planned-from-children")]
    public async Task<IActionResult> RecalculatePlannedFromChildrenAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.RecalculatePlannedFromChildrenAsync(id, ct));

    [HttpPost("{id}/recalculate-baseline-from-children")]
    public async Task<IActionResult> RecalculateBaselineFromChildrenAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.RecalculateBaselineFromChildrenAsync(id, ct));

    [HttpPost("{id}/planning-mode")]
    public async Task<IActionResult> SetPlanningModeAsync(Guid id, [FromBody] SetPlanningModeDto dto, CancellationToken ct) =>
        FromResult(await service.SetPlanningModeAsync(id, dto, ct));

    [HttpPut("{id}/estimated-work")]
    public async Task<IActionResult> UpdateEstimatedWorkAsync(Guid id, [FromBody] UpdateEstimatedWorkDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateEstimatedWorkAsync(id, dto, ct));

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> AddTagAsync(Guid id, [FromBody] AddProjectNodeTagDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddTagAsync(id, dto, ct), x => x.TagId);

    [HttpDelete("{id}/tags/{tagId}")]
    public async Task<IActionResult> RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct) =>
        FromResult(await service.RemoveTagAsync(id, tagId, ct));
}
