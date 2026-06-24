using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Http.Configuration;

[Route("api/config/bucketing")]
public sealed class BucketingController(IBucketingDefaultsService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<BucketingDefaultsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<BucketingDefaultsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateBucketingDefaultsDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(dto, ct));
}
