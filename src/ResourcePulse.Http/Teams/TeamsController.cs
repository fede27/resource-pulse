using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Teams;

namespace ResourcePulse.Http.Teams;

[Route("api/teams")]
public sealed class TeamsController(ITeamService service)
    : CrudController<CreateTeamDto, UpdateTeamDto, TeamReadDto, Guid>
{
    [HttpGet]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateTeamDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateTeamDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}
