using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public interface ILoadBandConfigurationService
{
    Task<ServiceResult<LoadBandConfigurationDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<LoadBandConfigurationDto>> UpdateAsync(UpdateLoadBandConfigurationDto dto, CancellationToken ct = default);

    // Internal read for the triage detector: OverloadFloor / HealthyFloor live on
    // the aggregate so the detector and the boards cannot disagree about where a
    // band starts — and so SignalPolicy never grows a second copy of a threshold
    // that already exists here (ADR-0032 §12).
    Task<LoadBandConfiguration> GetConfigurationAsync(CancellationToken ct = default);
}
