using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Demands;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Http.Load;

// Load endpoints live alongside the entities they describe — there is no
// "loads" resource. Both routes invoke ILoadQueryService.
[ApiController]
public sealed class LoadController(ILoadQueryService service) : ControllerFoundation
{
    // `status` optionally narrows to one commitment status (e.g. Hard for the
    // Allocazioni heatmap cells, which count committed blocks by default);
    // omitted = all blocks. Twin of the load-profile filter below.
    [HttpGet("api/resources/{id}/load")]
    [ProducesResponseType<IReadOnlyList<DailyLoadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourceLoadAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] AllocationStatus? status = null,
        CancellationToken ct = default) =>
        FromResult(await service.GetForResourceAsync(id, from, to, status, ct));

    // Commitment profile: run-length segments of the resource's committed rate%
    // over the horizon, decomposed by root project (ADR-0023 / gap #4+#10). Peak =
    // max(Percent) across segments; the peak's composition is the peak segment's
    // ByProject. Distinct from /load (capacity-normalised daily series).
    // `status` optionally narrows to one commitment status (e.g. Hard for the
    // sustainability verdict); omitted = all blocks.
    [HttpGet("api/resources/{id}/load-profile")]
    [ProducesResponseType<IReadOnlyList<LoadSegmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResourceLoadProfileAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] AllocationStatus? status = null,
        CancellationToken ct = default) =>
        FromResult(await service.GetCommitmentProfileForResourceAsync(id, from, to, status, ct));

    // Batch twin of /api/resources/{id}/load-profile (consolidation P2): the
    // commitment profiles of a whole population in one call. `ids` omitted/empty
    // = all active resources; explicit ids are honoured regardless of active
    // state; unknown ids are absent from the result. `status` as on the singular.
    // The literal segment wins over GET {id}/… by route precedence.
    [HttpGet("api/resources/load-profiles")]
    [ProducesResponseType<IReadOnlyList<ResourceLoadProfileDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetResourceLoadProfilesAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] AllocationStatus? status = null,
        [FromQuery] Guid[]? ids = null,
        CancellationToken ct = default) =>
        FromResult(await service.GetCommitmentProfilesForResourcesAsync(ids, from, to, status, ct));

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

    // Demand-vs-coverage over the node's subtree (Phase 5.2, ADR-0025/0026):
    // required/covered/gap hours per demand. Best-effort demands carry a null gap.
    [HttpGet("api/project-nodes/{id}/demand-coverage")]
    [ProducesResponseType<IReadOnlyList<DemandCoverageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectNodeDemandCoverageAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetDemandCoverageForProjectNodeAsync(id, from, to, ct));

    // Open demands across the whole plan (ADR-0027): reconciliation leaves a
    // residual (GapHours > 0) or the demand is best-effort; roots Closed/Cancelled
    // excluded (I4). `roleId` narrows to demands asking for that role — the
    // "cover free capacity" picker filters on the person's role. The literal
    // segment wins over GET api/demands/{id} by route precedence.
    [HttpGet("api/demands/open")]
    [ProducesResponseType<IReadOnlyList<OpenDemandDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOpenDemandsAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? roleId = null,
        CancellationToken ct = default) =>
        FromResult(await service.GetOpenDemandsAsync(roleId, from, to, ct));

    // Cross-project reconciliation (consolidation P4): every demand on a root
    // not Closed/Cancelled (I4), reconciled over the range in one call — the
    // batch twin of /api/project-nodes/{id}/demand-coverage; the board pivots
    // by the DTO-resolved RootProjectId client-side. The literal segment wins
    // over GET api/demands/{id} by route precedence (same as demands/open).
    [HttpGet("api/demands/coverage")]
    [ProducesResponseType<IReadOnlyList<DemandCoverageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDemandCoverageInRangeAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetDemandCoverageInRangeAsync(from, to, ct));

    // Coverage of a single demand over the range.
    [HttpGet("api/demands/{id}/coverage")]
    [ProducesResponseType<DemandCoverageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDemandCoverageAsync(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        FromResult(await service.GetDemandCoverageForDemandAsync(id, from, to, ct));
}
