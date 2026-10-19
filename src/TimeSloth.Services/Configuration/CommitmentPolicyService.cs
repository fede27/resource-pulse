using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Domain;
using TimeSloth.Common.Results;
using TimeSloth.Domain;
using TimeSloth.Domain.Configuration;
using TimeSloth.Persistence;
using TimeSloth.Services.Shared;

namespace TimeSloth.Services.Configuration;

// See LoadBandConfigurationService for why the DbContext is injected directly.
public sealed class CommitmentPolicyService(
    IRepository<CommitmentPolicyConfiguration, Guid> repository,
    TimeSlothDbContext db) : ICommitmentPolicyService
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

    // Per-tenant singleton get-or-seed (ADR-0029), race-safe (see SingletonSeed).
    public Task<CommitmentPolicyConfiguration> GetConfigurationAsync(CancellationToken ct = default) =>
        SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.CommitmentPolicies.FirstOrDefaultAsync(token),
            CommitmentPolicyConfiguration.CreateDefault,
            ct);

    private static CommitmentPolicyDto ToDto(CommitmentPolicyConfiguration config) => new()
    {
        HardCommitLevels = config.HardCommitLevels.ToList()
    };
}
