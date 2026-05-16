using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Ping;

public interface IPingService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<PingDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<PingDto>> CreateAsync(CreatePingDto dto, CancellationToken ct = default);
    Task<ServiceResult<PingDto>> UpdateAsync(Guid id, UpdatePingDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
