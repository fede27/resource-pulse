using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Http.Configuration;

[Route("api/config/commitment-policy")]
public sealed class CommitmentPolicyController(ICommitmentPolicyService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<CommitmentPolicyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<CommitmentPolicyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateCommitmentPolicyDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(dto, ct));
}
