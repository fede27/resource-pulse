using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Tags;

public interface ITagService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<TagReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<TagReadDto>> CreateAsync(CreateTagDto dto, CancellationToken ct = default);
    Task<ServiceResult<TagReadDto>> UpdateAsync(Guid id, UpdateTagDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
