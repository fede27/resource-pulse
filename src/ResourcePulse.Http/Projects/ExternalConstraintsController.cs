using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.ExternalConstraints;

namespace ResourcePulse.Http.Projects;

// Imposed dates on a root project (ADR-0034 §4) — a project artefact, so it
// lives beside the other project routes and not in the plan envelope. The one
// thing that IS plan mutation, moving the date while boundaries are anchored to
// it, is refused here (409 with the count) and done through the envelope's
// moveConstraint, where dryRun shows what moves.
[Route("api/projects/{rootId}/constraints")]
public sealed class ExternalConstraintsController(IExternalConstraintService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExternalConstraintReadDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForRootAsync(Guid rootId, CancellationToken ct) =>
        FromResult(await service.GetForRootAsync(rootId, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<ExternalConstraintReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid rootId, Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(rootId, id, ct));

    [RequirePlanner]
    [HttpPost]
    [ProducesResponseType<ExternalConstraintReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync(Guid rootId, [FromBody] CreateExternalConstraintDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(rootId, dto, ct), x => x.Id);

    [RequirePlanner]
    [HttpPut("{id}")]
    [ProducesResponseType<ExternalConstraintReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(Guid rootId, Guid id, [FromBody] UpdateExternalConstraintDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(rootId, id, dto, ct));

    [RequirePlanner]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(Guid rootId, Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(rootId, id, ct));
}
