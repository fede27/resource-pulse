using DevExtreme.AspNet.Data;
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
    public async Task<IActionResult> GetAllAsync([FromQuery] DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    // Aggregates over the node's subtree (node + descendants via Path prefix),
    // mirroring the allocation reads (ADR-0022).
    [HttpGet("by-project-node/{projectNodeId}")]
    public async Task<IActionResult> GetForProjectNodeAsync(Guid projectNodeId, CancellationToken ct) =>
        FromResult(await service.GetForProjectNodeAsync(projectNodeId, ct));
}
