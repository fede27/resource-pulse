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
