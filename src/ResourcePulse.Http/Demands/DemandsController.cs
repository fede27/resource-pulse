using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Demands;

namespace ResourcePulse.Http.Demands;

// READ side only (Phase 5.0). Demand mutation goes through the command envelope
// POST /api/plan/commands (createDemand / editDemand / deleteDemand / — in 5.1 —
// coverInferred). No write endpoints here (envelope discipline, ADR-0018).
[Route("api/demands")]
[ApiController]
public sealed class DemandsController(IDemandService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<DemandReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    // Aggregates over the node's subtree (node + descendants via Path prefix),
    // mirroring the allocation reads (ADR-0022).
    [HttpGet("by-project-node/{projectNodeId}")]
    [ProducesResponseType<IReadOnlyList<DemandReadDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForProjectNodeAsync(Guid projectNodeId, CancellationToken ct) =>
        FromResult(await service.GetForProjectNodeAsync(projectNodeId, ct));
}
