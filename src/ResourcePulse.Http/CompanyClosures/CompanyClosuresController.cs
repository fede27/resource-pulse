using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Services.CompanyClosures;

namespace ResourcePulse.Http.CompanyClosures;

[Route("api/company-closures")]
public sealed class CompanyClosuresController(ICompanyClosureService service)
    : CrudController<CreateCompanyClosureDto, UpdateCompanyClosureDto, CompanyClosureReadDto, Guid>
{
    [HttpGet]
    [ProducesResponseType<LoadResult>(StatusCodes.Status200OK)]
    public override async Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct) =>
        FromResult(await service.GetAllAsync(loadOptions, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<CompanyClosureReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<CompanyClosureReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public override async Task<IActionResult> CreateAsync([FromBody] CreateCompanyClosureDto dto, CancellationToken ct) =>
        FromCreateResult(await service.CreateAsync(dto, ct), x => x.Id);

    [HttpPut("{id}")]
    [ProducesResponseType<CompanyClosureReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateCompanyClosureDto dto, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public override async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}
