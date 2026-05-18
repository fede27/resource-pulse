using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Skills;

public interface ISkillService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<SkillReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<SkillReadDto>> CreateAsync(CreateSkillDto dto, CancellationToken ct = default);
    Task<ServiceResult<SkillReadDto>> UpdateAsync(Guid id, UpdateSkillDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
