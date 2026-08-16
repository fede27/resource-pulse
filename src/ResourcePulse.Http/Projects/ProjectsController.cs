using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Http.Projects;

// Sugar routes for operations the domain restricts to root Project nodes:
// metadata mutation (type/commitment/lead), state machine transitions, and skill
// requirements. The service layer enforces "must be a root" and returns NotFound
// when given a Phase or WorkPackage id.
[Route("api/projects")]
public sealed class ProjectsController(IProjectNodeService service) : ControllerFoundation
{
    [RequirePlanner]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProjectAsync(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateProjectAsync(id, dto, ct));

    // State transitions
    [RequirePlanner]
    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.StartAsync(id, ct));

    [RequirePlanner]
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.CompleteAsync(id, ct));

    [RequirePlanner]
    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> SuspendAsync(Guid id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        FromResult(await service.SuspendAsync(id, dto, ct));

    [RequirePlanner]
    [HttpPost("{id}/resume")]
    public async Task<IActionResult> ResumeAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.ResumeAsync(id, ct));

    [RequirePlanner]
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(Guid id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        FromResult(await service.CancelAsync(id, dto, ct));

    // Skill requirements
    [RequirePlanner]
    [HttpPost("{id}/skill-requirements")]
    public async Task<IActionResult> AddSkillRequirementAsync(Guid id, [FromBody] AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddSkillRequirementAsync(id, dto, ct), x => x.SkillId);

    [RequirePlanner]
    [HttpPut("{id}/skill-requirements/{skillId}")]
    public async Task<IActionResult> UpdateSkillRequirementAsync(Guid id, Guid skillId, [FromBody] AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateSkillRequirementLevelAsync(id, skillId, dto, ct));

    [RequirePlanner]
    [HttpDelete("{id}/skill-requirements/{skillId}")]
    public async Task<IActionResult> RemoveSkillRequirementAsync(Guid id, Guid skillId, CancellationToken ct) =>
        FromResult(await service.RemoveSkillRequirementAsync(id, skillId, ct));

    // "Active projects in range" — DateSource selects which date set (Planned / Baseline / Effective).
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProjectNodeReadDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveInRangeAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] DateSource dateSource = DateSource.Planned,
        CancellationToken ct = default) =>
        FromResult(await service.GetProjectsActiveInRangeAsync(from, to, dateSource, ct));
}
