using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Identity;

namespace ResourcePulse.Http.Identity;

// Current-user identity for the frontend (gap #8 / ADR-0024): drives the
// "my projects" / "my open roles" filters.
[Route("api/me")]
[ApiController]
public sealed class MeController(IMeService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));
}
