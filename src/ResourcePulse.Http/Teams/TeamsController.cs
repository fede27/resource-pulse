using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Teams;

namespace ResourcePulse.Http.Teams;

[Route("api/teams")]
public sealed class TeamsController(ITeamService service)
    : CrudController<CreateTeamDto, UpdateTeamDto, TeamReadDto, Guid>
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<TeamReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<TeamReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateTeamDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    [ProducesResponseType<TeamReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateTeamDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}
