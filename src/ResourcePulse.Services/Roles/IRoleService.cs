using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Roles;

public interface IRoleService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<RoleReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<RoleReadDto>> CreateAsync(CreateRoleDto dto, CancellationToken ct = default);
    Task<ServiceResult<RoleReadDto>> UpdateAsync(Guid id, UpdateRoleDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
