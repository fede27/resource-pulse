using DevExtreme.AspNet.Data;
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
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateResourceDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateResourceDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id}/work-windows")]
    public async Task<IActionResult> AddWorkWindowAsync(Guid id, [FromBody] WorkWindowDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddWorkWindowAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/work-windows/{windowId}")]
    public async Task<IActionResult> RemoveWorkWindowAsync(Guid id, Guid windowId, CancellationToken ct) =>
        FromResult(await service.RemoveWorkWindowAsync(id, windowId, ct));

    [HttpPost("{id}/adjustments")]
    public async Task<IActionResult> AddAdjustmentAsync(Guid id, [FromBody] IndividualAdjustmentDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddAdjustmentAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/adjustments/{adjustmentId}")]
    public async Task<IActionResult> RemoveAdjustmentAsync(Guid id, Guid adjustmentId, CancellationToken ct) =>
        FromResult(await service.RemoveAdjustmentAsync(id, adjustmentId, ct));

    [HttpGet("{id}/capacity")]
    public async Task<IActionResult> GetCapacityAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await capacityService.GetForResourceAsync(id, from, to, ct));
}
