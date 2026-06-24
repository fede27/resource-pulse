using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Allocations;

namespace ResourcePulse.Http.Allocations;

// READ side only. All plan mutation moved to the command envelope
// POST /api/plan/commands (ADR-0018) — the fine-grained write endpoints
// (by-percent, by-hours, placeholder, PUT, move, convert-to-placeholder,
// assign, status, split, change-rate-from, shift, resize, shift-from, DELETE)
// are retired. "Non si tengono due forme."
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
}
