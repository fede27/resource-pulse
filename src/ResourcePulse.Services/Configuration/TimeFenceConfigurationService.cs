using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public sealed class TimeFenceConfigurationService(
    IRepository<TimeFenceConfiguration, Guid> repository) : ITimeFenceConfigurationService
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

    private async Task<TimeFenceConfiguration> GetOrSeedAsync(CancellationToken ct)
    {
        var config = await repository.GetByIdAsync(TimeFenceConfiguration.SingletonId, ct);
        if (config is null)
        {
            config = TimeFenceConfiguration.CreateDefault();
            await repository.AddAsync(config, ct);
            await repository.SaveChangesAsync(ct);
        }
        return config;
    }

    private static TimeFenceConfigurationDto ToDto(TimeFenceConfiguration config) => new()
    {
        FrozenHorizon = new DurationDto { Value = config.FrozenHorizon.Value, Unit = config.FrozenHorizon.Unit },
        SlushyHorizon = new DurationDto { Value = config.SlushyHorizon.Value, Unit = config.SlushyHorizon.Unit }
    };
}
