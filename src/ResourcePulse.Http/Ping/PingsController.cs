using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.Ping;

namespace ResourcePulse.Http.Ping;

[Route("api/pings")]
public sealed class PingsController(IPingService pingService) : CrudController<CreatePingDto, UpdatePingDto, PingDto, Guid>
{
    [HttpGet]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await pingService.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await pingService.GetByIdAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreatePingDto dto, CancellationToken ct) =>
        FromCreateResult(await pingService.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdatePingDto dto, CancellationToken ct) =>
        FromResult(await pingService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await pingService.DeleteAsync(id, ct));
}
