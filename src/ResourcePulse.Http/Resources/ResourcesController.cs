using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Resources;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Http.Resources;

[Route("api/resources")]
public sealed class ResourcesController(
    IResourceService service,
    ICapacityQueryService capacityService)
    : CrudController<CreateResourceDto, UpdateResourceDto, ResourceReadDto, Guid>
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<ResourceReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<ResourceReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateResourceDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    [ProducesResponseType<ResourceReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateResourceDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id}/work-windows")]
    [ProducesResponseType<WorkWindowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddWorkWindowAsync(Guid id, [FromBody] WorkWindowDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddWorkWindowAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/work-windows/{windowId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWorkWindowAsync(Guid id, Guid windowId, CancellationToken ct) =>
        FromResult(await service.RemoveWorkWindowAsync(id, windowId, ct));

    [HttpPost("{id}/adjustments")]
    [ProducesResponseType<IndividualAdjustmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAdjustmentAsync(Guid id, [FromBody] IndividualAdjustmentDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddAdjustmentAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/adjustments/{adjustmentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAdjustmentAsync(Guid id, Guid adjustmentId, CancellationToken ct) =>
        FromResult(await service.RemoveAdjustmentAsync(id, adjustmentId, ct));

    [HttpPut("{id}/team")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTeamAsync(Guid id, [FromBody] AssignTeamDto dto, CancellationToken ct) =>
        FromResult(await service.AssignTeamAsync(id, dto, ct));

    [HttpPut("{id}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoleAsync(Guid id, [FromBody] AssignRoleDto dto, CancellationToken ct) =>
        FromResult(await service.AssignRoleAsync(id, dto, ct));

    [HttpPost("{id}/skills")]
    [ProducesResponseType<ResourceSkillDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSkillAsync(Guid id, [FromBody] AddOrUpdateResourceSkillDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddSkillAsync(id, dto, ct), x => x.SkillId);

    [HttpPut("{id}/skills/{skillId}")]
    [ProducesResponseType<ResourceSkillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSkillLevelAsync(Guid id, Guid skillId, [FromBody] AddOrUpdateResourceSkillDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateSkillLevelAsync(id, skillId, dto, ct));

    [HttpDelete("{id}/skills/{skillId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSkillAsync(Guid id, Guid skillId, CancellationToken ct) =>
        FromResult(await service.RemoveSkillAsync(id, skillId, ct));

    [HttpPost("{id}/skills/{skillId}/approve")]
    [ProducesResponseType<ResourceSkillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveSkillAsync(Guid id, Guid skillId, CancellationToken ct) =>
        FromResult(await service.ApproveSkillAsync(id, skillId, ct));

    [HttpPost("{id}/skills/{skillId}/reject")]
    [ProducesResponseType<ResourceSkillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectSkillAsync(Guid id, Guid skillId, CancellationToken ct) =>
        FromResult(await service.RejectSkillAsync(id, skillId, ct));

    [HttpPost("{id}/skills/{skillId}/return-to-pending")]
    [ProducesResponseType<ResourceSkillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnSkillToPendingAsync(Guid id, Guid skillId, CancellationToken ct) =>
        FromResult(await service.ReturnSkillToPendingAsync(id, skillId, ct));

    [HttpPost("{id}/tags")]
    [ProducesResponseType<ResourceTagDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTagAsync(Guid id, [FromBody] AddResourceTagDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddTagAsync(id, dto, ct), x => x.TagId);

    [HttpDelete("{id}/tags/{tagId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct) =>
        FromResult(await service.RemoveTagAsync(id, tagId, ct));

    [HttpGet("{id}/capacity")]
    [ProducesResponseType<IReadOnlyList<DailyCapacityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCapacityAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await capacityService.GetForResourceAsync(id, from, to, ct));
}
