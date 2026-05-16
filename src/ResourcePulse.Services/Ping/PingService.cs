using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;

namespace ResourcePulse.Services.Ping;

public sealed class PingService(IRepository<Domain.Ping, Guid> repository, IMapper mapper) : IPingService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<PingDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<PingDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(p => p.Id == id)
            .ProjectToType<PingDto>()
            .FirstOrDefaultAsync(ct);

        if (dto is null) return ServiceResult<PingDto>.NotFound($"Ping {id} not found.");
        return ServiceResult<PingDto>.Success(dto);
    }

    public async Task<ServiceResult<PingDto>> CreateAsync(CreatePingDto dto, CancellationToken ct = default)
    {
        var ping = new Domain.Ping(Guid.NewGuid()) { Message = dto.Message };
        await repository.AddAsync(ping, ct);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<PingDto>.Success(mapper.Map<PingDto>(ping));
    }

    public async Task<ServiceResult<PingDto>> UpdateAsync(Guid id, UpdatePingDto dto, CancellationToken ct = default)
    {
        var ping = await repository.GetByIdAsync(id, ct);
        if (ping is null) return ServiceResult<PingDto>.NotFound($"Ping {id} not found.");

        ping.Message = dto.Message;
        await repository.SaveChangesAsync(ct);
        return ServiceResult<PingDto>.Success(mapper.Map<PingDto>(ping));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ping = await repository.GetByIdAsync(id, ct);
        if (ping is null) return ServiceResult<Unit>.NotFound($"Ping {id} not found.");

        repository.Remove(ping);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
