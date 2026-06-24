using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Configuration;

public interface ITimeFenceConfigurationService
{
    Task<ServiceResult<TimeFenceConfigurationDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<TimeFenceConfigurationDto>> UpdateAsync(UpdateTimeFenceConfigurationDto dto, CancellationToken ct = default);
}
