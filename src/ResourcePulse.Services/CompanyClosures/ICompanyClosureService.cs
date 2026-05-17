using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.CompanyClosures;

public interface ICompanyClosureService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<CompanyClosureReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<CompanyClosureReadDto>> CreateAsync(CreateCompanyClosureDto dto, CancellationToken ct = default);
    Task<ServiceResult<CompanyClosureReadDto>> UpdateAsync(Guid id, UpdateCompanyClosureDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
