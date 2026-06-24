using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public sealed class CommitmentPolicyService(
    IRepository<CommitmentPolicyConfiguration, Guid> repository) : ICommitmentPolicyService
{
    public async Task<ServiceResult<CommitmentPolicyDto>> GetAsync(CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync(ct);
        return ServiceResult<CommitmentPolicyDto>.Success(ToDto(config));
    }

    public async Task<ServiceResult<CommitmentPolicyDto>> UpdateAsync(
        UpdateCommitmentPolicyDto dto, CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync(ct);

        try
        {
            config.Replace(dto.HardCommitLevels);
        }
        catch (DomainException ex)
        {
            return ServiceResult<CommitmentPolicyDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(UpdateCommitmentPolicyDto.HardCommitLevels)] = [ex.Message]
            });
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<CommitmentPolicyDto>.Success(ToDto(config));
    }

    public async Task<CommitmentPolicyConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await repository.GetByIdAsync(CommitmentPolicyConfiguration.SingletonId, ct);
        if (config is null)
        {
            config = CommitmentPolicyConfiguration.CreateDefault();
            await repository.AddAsync(config, ct);
            await repository.SaveChangesAsync(ct);
        }
        return config;
    }

    private static CommitmentPolicyDto ToDto(CommitmentPolicyConfiguration config) => new()
    {
        HardCommitLevels = config.HardCommitLevels.ToList()
    };
}
