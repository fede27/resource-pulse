using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Allocations;

namespace ResourcePulse.Http.Allocations;

[Route("api/allocations")]
public sealed class AllocationsController(IAllocationService service)
    : CrudController<CreateAllocationDto, UpdateAllocationDto, AllocationReadDto, Guid>
{
    [HttpGet]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpGet("by-resource/{resourceId}")]
    public async Task<IActionResult> GetForResourceAsync(
        Guid resourceId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForResourceAsync(resourceId, from, to, ct));

    [HttpGet("by-project-node/{projectNodeId}")]
    public async Task<IActionResult> GetForProjectNodeAsync(
        Guid projectNodeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForProjectNodeAsync(projectNodeId, from, to, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateAllocationDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateAllocationDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}
