using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Http.Configuration;

[Route("api/config/time-fence")]
public sealed class TimeFenceController(ITimeFenceConfigurationService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<TimeFenceConfigurationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<TimeFenceConfigurationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateTimeFenceConfigurationDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(dto, ct));
}
