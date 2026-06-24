using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public interface ICommitmentPolicyService
{
    Task<ServiceResult<CommitmentPolicyDto>> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<CommitmentPolicyDto>> UpdateAsync(UpdateCommitmentPolicyDto dto, CancellationToken ct = default);

    // Internal read for the I6 callers (PlanCommandService hard gate,
    // ProjectNodeService cascade-demotion threshold). Returns the get-or-seeded
    // aggregate so the threshold logic (IsHardCommitted) has a single home.
    Task<CommitmentPolicyConfiguration> GetConfigurationAsync(CancellationToken ct = default);
}
