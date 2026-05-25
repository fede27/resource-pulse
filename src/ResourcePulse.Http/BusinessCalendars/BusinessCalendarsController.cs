using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.BusinessCalendars;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Http.BusinessCalendars;

[Route("api/business-calendars")]
public sealed class BusinessCalendarsController(IBusinessCalendarService service)
    : CrudController<CreateBusinessCalendarDto, UpdateBusinessCalendarDto, BusinessCalendarReadDto, Guid>
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<BusinessCalendarReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<BusinessCalendarReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateBusinessCalendarDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    [ProducesResponseType<BusinessCalendarReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateBusinessCalendarDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id}/work-windows")]
    [ProducesResponseType<WorkWindowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddWorkWindowAsync(Guid id, [FromBody] WorkWindowDto dto, CancellationToken ct) =>
        FromCreateResult(await service.AddWorkWindowAsync(id, dto, ct), x => x.Id);

    [HttpDelete("{id}/work-windows/{windowId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWorkWindowAsync(Guid id, Guid windowId, CancellationToken ct) =>
        FromResult(await service.RemoveWorkWindowAsync(id, windowId, ct));

    [HttpPost("{id}/mark-default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsDefaultAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.MarkAsDefaultAsync(id, ct));
}
