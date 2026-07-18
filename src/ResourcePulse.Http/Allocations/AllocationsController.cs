using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Allocations;

namespace ResourcePulse.Http.Allocations;

// READ side only. All plan mutation moved to the command envelope
// POST /api/plan/commands (ADR-0018). Coverage model (Phase 5.1, ADR-0025):
// every allocation is a coverage with a real resource pointing at a demand.
[Route("api/allocations")]
[ApiController]
public sealed class AllocationsController(IAllocationService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<AllocationReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpGet("by-resource/{resourceId}")]
    [ProducesResponseType<IReadOnlyList<AllocationReadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForResourceAsync(
        Guid resourceId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForResourceAsync(resourceId, from, to, ct));

    // Aggregates over the node's subtree (node + descendants via Path prefix),
    // not the exact node — phase-level coverage is included (ADR-0022 / gap #5).
    [HttpGet("by-project-node/{projectNodeId}")]
    [ProducesResponseType<IReadOnlyList<AllocationReadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForProjectNodeAsync(
        Guid projectNodeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForProjectNodeAsync(projectNodeId, from, to, ct));

    // The flat plan slice (consolidation P3): every coverage overlapping the
    // range. Boards read this once and pivot client-side (by root project on
    // Progetti, by resource on Persone). The literal segment wins over
    // GET {id} by route precedence (same pattern as api/demands/open).
    [HttpGet("in-range")]
    [ProducesResponseType<IReadOnlyList<AllocationReadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInRangeAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetInRangeAsync(from, to, ct));

    [HttpGet("{id}/resolved-hours")]
    [ProducesResponseType<AllocationResolvedHoursDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetResolvedHoursAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetResolvedHoursAsync(id, ct));
}
