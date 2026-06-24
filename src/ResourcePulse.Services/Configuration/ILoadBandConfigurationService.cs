using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Configuration;

public interface ILoadBandConfigurationService
{
    Task<ServiceResult<LoadBandConfigurationDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<LoadBandConfigurationDto>> UpdateAsync(UpdateLoadBandConfigurationDto dto, CancellationToken ct = default);
}
