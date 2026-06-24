using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Http.Plan;

// Single plan-mutation endpoint (ADR-0018). The body is a typed command — a
// discriminated union with the "kind" discriminator. One mechanism, many
// intents. dryRun on the command returns the computed consequence without
// committing. New gestures arrive as new command kinds, not new endpoints.
//
// The per-command FluentValidation validators are resolved by the
// DtoValidationFilter on the runtime type of the bound body.
[Route("api/plan")]
[ApiController]
public sealed class PlanCommandsController(IPlanCommandService service) : ControllerFoundation
{
    [HttpPost("commands")]
    public async Task<IActionResult> ExecuteAsync([FromBody] PlanCommand command, CancellationToken ct) =>
        FromResult(await service.ExecuteAsync(command, ct));
}
