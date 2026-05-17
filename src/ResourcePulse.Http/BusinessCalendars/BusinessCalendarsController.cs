using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.BusinessCalendars;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Http.BusinessCalendars;

[Route("api/business-calendars")]
public sealed class BusinessCalendarsController(IBusinessCalendarService service)
    : CrudController<CreateBusinessCalendarDto, UpdateBusinessCalendarDto, BusinessCalendarReadDto, Guid>
{
    [HttpGet]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateBusinessCalendarDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateBusinessCalendarDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id}/work-windows")]
    public async Task<IActionResult> AddWorkWindowAsync(Guid id, [FromBody] WorkWindowDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddWorkWindowAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/work-windows/{windowId}")]
    public async Task<IActionResult> RemoveWorkWindowAsync(Guid id, Guid windowId, CancellationToken ct) =>
        FromResult(await service.RemoveWorkWindowAsync(id, windowId, ct));

    [HttpPost("{id}/mark-default")]
    public async Task<IActionResult> MarkAsDefaultAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.MarkAsDefaultAsync(id, ct));
}
