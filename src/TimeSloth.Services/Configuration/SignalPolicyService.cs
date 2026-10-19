using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Domain;
using TimeSloth.Common.Results;
using TimeSloth.Domain;
using TimeSloth.Domain.Configuration;
using TimeSloth.Domain.Signals;
using TimeSloth.Persistence;
using TimeSloth.Services.Shared;

namespace TimeSloth.Services.Configuration;

// See LoadBandConfigurationService for why the DbContext is injected directly.
public sealed class SignalPolicyService(
    IRepository<SignalPolicy, Guid> repository,
    TimeSlothDbContext db) : ISignalPolicyService
{
    public async Task<ServiceResult<SignalPolicyDto>> GetAsync(CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync(ct);
        return ServiceResult<SignalPolicyDto>.Success(ToDto(config));
    }

    public async Task<ServiceResult<SignalPolicyDto>> UpdateAsync(
        UpdateSignalPolicyDto dto, CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync(ct);

        try
        {
            config.Replace(
                dto.ResolvedRetentionDays,
                Duration.Of(dto.DecisionLeadTime.Value, dto.DecisionLeadTime.Unit));
        }
        catch (DomainException ex)
        {
            return ServiceResult<SignalPolicyDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(UpdateSignalPolicyDto.ResolvedRetentionDays)] = [ex.Message]
            });
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<SignalPolicyDto>.Success(ToDto(config));
    }

    // Per-tenant singleton get-or-seed (ADR-0029), race-safe (see SingletonSeed).
    public Task<SignalPolicy> GetConfigurationAsync(CancellationToken ct = default) =>
        SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.SignalPolicies.FirstOrDefaultAsync(token),
            SignalPolicy.CreateDefault,
            ct);

    private static SignalPolicyDto ToDto(SignalPolicy config) => new()
    {
        ResolvedRetentionDays = config.ResolvedRetentionDays,
        DecisionLeadTime = new DurationDto
        {
            Value = config.DecisionLeadTime.Value,
            Unit = config.DecisionLeadTime.Unit
        },
        // Read-only echo of the declared constant, so no client hard-codes 7.
        QueueBudget = SignalRanking.QueueBudget
    };
}
