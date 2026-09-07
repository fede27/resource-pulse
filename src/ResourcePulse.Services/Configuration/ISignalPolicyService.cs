using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public interface ISignalPolicyService
{
    Task<ServiceResult<SignalPolicyDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<SignalPolicyDto>> UpdateAsync(UpdateSignalPolicyDto dto, CancellationToken ct = default);

    // Internal read for the detector: the derived-deadline lead time and the
    // retention window live on the aggregate, so DeriveDeadline has a single home
    // — same shape as ICommitmentPolicyService.GetConfigurationAsync for I6.
    Task<SignalPolicy> GetConfigurationAsync(CancellationToken ct = default);
}
