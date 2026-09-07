using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Configuration;

// See LoadBandConfigurationService for why the DbContext is injected directly.
public sealed class TimeFenceConfigurationService(
    IRepository<TimeFenceConfiguration, Guid> repository,
    ResourcePulseDbContext db) : ITimeFenceConfigurationService
{
    public async Task<ServiceResult<TimeFenceConfigurationDto>> GetAsync(CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);
        return ServiceResult<TimeFenceConfigurationDto>.Success(ToDto(config));
    }

    public async Task<ServiceResult<TimeFenceConfigurationDto>> UpdateAsync(
        UpdateTimeFenceConfigurationDto dto, CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);

        try
        {
            config.Replace(
                Duration.Of(dto.FrozenHorizon.Value, dto.FrozenHorizon.Unit),
                Duration.Of(dto.SlushyHorizon.Value, dto.SlushyHorizon.Unit));
        }
        catch (DomainException ex)
        {
            return ServiceResult<TimeFenceConfigurationDto>.Validation(new Dictionary<string, string[]>
            {
                ["TimeFence"] = [ex.Message]
            });
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<TimeFenceConfigurationDto>.Success(ToDto(config));
    }

    // Per-tenant singleton get-or-seed (ADR-0029).
    // Exposes the get-or-seeded aggregate to the triage detector, which needs the
    // aggregate's own derivations (ComputeBoundaries) rather than the DTO's raw
    // durations.
    public Task<TimeFenceConfiguration> GetConfigurationAsync(CancellationToken ct = default) =>
        GetOrSeedAsync(ct);

    private Task<TimeFenceConfiguration> GetOrSeedAsync(CancellationToken ct) =>
        SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.TimeFenceConfigurations.FirstOrDefaultAsync(token),
            TimeFenceConfiguration.CreateDefault,
            ct);

    private static TimeFenceConfigurationDto ToDto(TimeFenceConfiguration config) => new()
    {
        FrozenHorizon = new DurationDto { Value = config.FrozenHorizon.Value, Unit = config.FrozenHorizon.Unit },
        SlushyHorizon = new DurationDto { Value = config.SlushyHorizon.Value, Unit = config.SlushyHorizon.Unit }
    };
}
