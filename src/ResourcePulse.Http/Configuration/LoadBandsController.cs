using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Http.Configuration;

// Org-level singleton config (ADR-0020): read + replace, no id, no collection.
[Route("api/config/load-bands")]
public sealed class LoadBandsController(ILoadBandConfigurationService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<LoadBandConfigurationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<LoadBandConfigurationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateLoadBandConfigurationDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(dto, ct));
}
