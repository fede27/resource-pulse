using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Configuration;

public interface IBucketingDefaultsService
{
    Task<ServiceResult<BucketingDefaultsDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<BucketingDefaultsDto>> UpdateAsync(UpdateBucketingDefaultsDto dto, CancellationToken ct = default);
}
