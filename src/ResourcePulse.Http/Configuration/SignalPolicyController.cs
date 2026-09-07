using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Http.Configuration;

// The fifth org-level configuration singleton (ADR-0032 §12). Exactly two dials:
// the depth of the change feed, and how much notice a staffing decision needs.
//
// What is NOT here is the point: the queue budget is a declared constant (it
// exists to force triage), the band thresholds already live in
// /api/config/load-bands, the set of active kinds is not configurable (an org
// that could switch off Overcommit would have a dashboard that lies), and the
// sweep cadence is operational rather than organizational.
[Route("api/config/signal-policy")]
public sealed class SignalPolicyController(ISignalPolicyService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<SignalPolicyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [RequireOwner]
    [HttpPut]
    [ProducesResponseType<SignalPolicyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateSignalPolicyDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(dto, ct));
}
