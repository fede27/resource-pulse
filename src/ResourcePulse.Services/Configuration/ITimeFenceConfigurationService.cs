using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public interface ITimeFenceConfigurationService
{
    Task<ServiceResult<TimeFenceConfigurationDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<TimeFenceConfigurationDto>> UpdateAsync(UpdateTimeFenceConfigurationDto dto, CancellationToken ct = default);

    // Internal read for the triage detector (ADR-0033): the boundaries are a
    // rolling projection computed by the aggregate, so ComputeBoundaries keeps a
    // single home rather than being re-derived from the DTO's durations. Same
    // shape as ICommitmentPolicyService.GetConfigurationAsync for I6.
    Task<TimeFenceConfiguration> GetConfigurationAsync(CancellationToken ct = default);
}
