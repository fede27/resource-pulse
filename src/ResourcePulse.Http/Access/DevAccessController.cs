using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Domain.Access;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Http.Access;

/// <summary>
/// Development-only role switching (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// <b>Registered only in Development</b>, by an environment check in
/// <c>Program.cs</c> that removes this controller from the application parts —
/// not by a compile-time <c>#if</c>, which would make the guarantee depend on how
/// the assembly was built rather than on how the process is running.
/// </para>
/// <para>
/// Guarded by the <b>Viewer</b> policy, not Owner, deliberately: it must stay
/// reachable after you have demoted yourself, or the switch is one-way. Being
/// unreachable outside Development is what makes that acceptable.
/// </para>
/// </remarks>
[Route("api/dev/access")]
[ApiController]
[Auth.DevelopmentOnly]
[Auth.RequireViewer]
public sealed class DevAccessController(IDevAccessService service) : ControllerFoundation
{
    [HttpPost("act-as")]
    [ProducesResponseType<AppRole>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActAsAsync([FromBody] DevActAsDto dto, CancellationToken ct) =>
        FromResult(await service.ActAsAsync(dto.Role, ct));
}
