using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Teams;

public interface ITeamService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<TeamReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<TeamReadDto>> CreateAsync(CreateTeamDto dto, CancellationToken ct = default);
    Task<ServiceResult<TeamReadDto>> UpdateAsync(Guid id, UpdateTeamDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
