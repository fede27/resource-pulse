using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Allocations;

namespace ResourcePulse.Http.Allocations;

// Two vocabularies for assigned creation:
//   POST /api/allocations/by-percent — rate-shaped ("Tizio at 50%")
//   POST /api/allocations/by-hours   — quantity-shaped ("Tizio 20h over 2 weeks")
// Placeholder creation (ADR-0016):
//   POST /api/allocations/placeholder/by-percent
//
// Editing in place: PUT /api/allocations/{id} (percent + notes; keep window).
// Moving the window:  POST /api/allocations/{id}/move with mode = KeepPercent | KeepHours.
// Sidecar: GET /api/allocations/{id}/resolved-hours for cheap per-row UI badges.
//
// Placeholder lifecycle transitions (ADR-0016):
//   POST /api/allocations/{id}/convert-to-placeholder — assigned → placeholder
//   POST /api/allocations/{id}/assign                 — placeholder → assigned
//
// Status (ADR-0015):
//   POST /api/allocations/{id}/status — promote/demote Tentative ↔ Hard
//
// The old polymorphic POST /api/allocations from Phase 4 is intentionally gone —
// see ADR-0013.
[Route("api/allocations")]
[ApiController]
public sealed class AllocationsController(IAllocationService service) : ControllerFoundation
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
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

    [HttpGet("{id}/resolved-hours")]
    public async Task<IActionResult> GetResolvedHoursAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetResolvedHoursAsync(id, ct));

    [HttpPost("by-percent")]
    public async Task<IActionResult> CreateByPercentAsync([FromBody] CreateByPercentDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateByPercentAsync(dto, ct), x => x.Id);

    [HttpPost("by-hours")]
    public async Task<IActionResult> CreateByHoursAsync([FromBody] CreateByHoursDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateByHoursAsync(dto, ct), x => x.Id);

    [HttpPost("placeholder/by-percent")]
    public async Task<IActionResult> CreatePlaceholderByPercentAsync(
        [FromBody] CreatePlaceholderByPercentDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreatePlaceholderByPercentAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateAllocationDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpPost("{id}/move")]
    public async Task<IActionResult> MoveAsync(Guid id, [FromBody] MoveAllocationDto dto, CancellationToken ct) =>
        FromResult(await service.MoveAsync(id, dto, ct));

    [HttpPost("{id}/convert-to-placeholder")]
    public async Task<IActionResult> ConvertToPlaceholderAsync(
        Guid id, [FromBody] ConvertToPlaceholderDto dto, CancellationToken ct) =>
        FromResult(await service.ConvertToPlaceholderAsync(id, dto, ct));

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignToResourceAsync(
        Guid id, [FromBody] AssignToResourceDto dto, CancellationToken ct) =>
        FromResult(await service.AssignToResourceAsync(id, dto, ct));

    [HttpPost("{id}/status")]
    public async Task<IActionResult> ChangeStatusAsync(
        Guid id, [FromBody] ChangeAllocationStatusDto dto, CancellationToken ct) =>
        FromResult(await service.ChangeStatusAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}
