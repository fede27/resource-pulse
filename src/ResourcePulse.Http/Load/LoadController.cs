using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Http.Load;

// Load endpoints live alongside the entities they describe — there is no
// "loads" resource. Both routes invoke ILoadQueryService.
[ApiController]
public sealed class LoadController(ILoadQueryService service) : ControllerFoundation
{
    [HttpGet("api/resources/{id}/load")]
    [ProducesResponseType<IReadOnlyList<DailyLoadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourceLoadAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForResourceAsync(id, from, to, ct));

    // Commitment profile: run-length segments of the resource's committed rate%
    // over the horizon, decomposed by root project (ADR-0023 / gap #4+#10). Peak =
    // max(Percent) across segments; the peak's composition is the peak segment's
    // ByProject. Distinct from /load (capacity-normalised daily series).
    [HttpGet("api/resources/{id}/load-profile")]
    [ProducesResponseType<IReadOnlyList<LoadSegmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourceLoadProfileAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetCommitmentProfileForResourceAsync(id, from, to, ct));

    // Aggregates over the node's subtree (node + descendants via Path prefix),
    // not the exact node — a project that staffs its Phases includes them here
    // (ADR-0022 / gap #5).
    [HttpGet("api/project-nodes/{id}/load")]
    [ProducesResponseType<IReadOnlyList<DailyNodeLoadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectNodeLoadAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetForProjectNodeAsync(id, from, to, ct));
}
