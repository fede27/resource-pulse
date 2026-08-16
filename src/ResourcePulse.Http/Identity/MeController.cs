using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Identity;

namespace ResourcePulse.Http.Identity;

// Current-user identity for the frontend (gap #8 / ADR-0024): drives the
// "my projects" / "my open roles" filters and every capability gate in the UI.
[Route("api/me")]
[ApiController]
// THE membership exemption (ADR-0030). Authentication only, deliberately: this is
// the one endpoint a non-member must still reach, because it is what tells the
// client to render "you have no access" instead of an empty application. Putting
// it behind the Viewer fallback turns that explanation into a blank screen.
// Any [Authorize] here suppresses the fallback — that is the mechanism, not a
// side effect.
[Authorize]
public sealed class MeController(IMeService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));
}
