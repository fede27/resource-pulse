using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Services.CompanyClosures;

public sealed class CompanyClosureService(
    IRepository<CompanyClosure, Guid> repository,
    IMapper mapper) : ICompanyClosureService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<CompanyClosureReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<CompanyClosureReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(c => c.Id == id)
            .ProjectToType<CompanyClosureReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<CompanyClosureReadDto>.NotFound($"CompanyClosure {id} not found.")
            : ServiceResult<CompanyClosureReadDto>.Success(dto);
    }

    public async Task<ServiceResult<CompanyClosureReadDto>> CreateAsync(
        CreateCompanyClosureDto dto,
        CancellationToken ct = default)
    {
        var closure = CompanyClosure.Create(dto.DateFrom, dto.DateTo, dto.Reason);
        await repository.AddAsync(closure, ct);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<CompanyClosureReadDto>.Success(mapper.Map<CompanyClosureReadDto>(closure));
    }

    public async Task<ServiceResult<CompanyClosureReadDto>> UpdateAsync(
        Guid id,
        UpdateCompanyClosureDto dto,
        CancellationToken ct = default)
    {
        var closure = await repository.GetByIdAsync(id, ct);
        if (closure is null)
            return ServiceResult<CompanyClosureReadDto>.NotFound($"CompanyClosure {id} not found.");

        closure.Update(dto.DateFrom, dto.DateTo, dto.Reason);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<CompanyClosureReadDto>.Success(mapper.Map<CompanyClosureReadDto>(closure));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var closure = await repository.GetByIdAsync(id, ct);
        if (closure is null) return ServiceResult.NotFound($"CompanyClosure {id} not found.");

        repository.Remove(closure);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
