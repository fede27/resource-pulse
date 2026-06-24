using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public sealed class LoadBandConfigurationService(
    IRepository<LoadBandConfiguration, Guid> repository) : ILoadBandConfigurationService
{
    public async Task<ServiceResult<LoadBandConfigurationDto>> GetAsync(CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);
        return ServiceResult<LoadBandConfigurationDto>.Success(ToDto(config));
    }

    public async Task<ServiceResult<LoadBandConfigurationDto>> UpdateAsync(
        UpdateLoadBandConfigurationDto dto, CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);

        try
        {
            config.Replace(dto.Bands.Select(b => (b.Label, b.LowerBound)).ToList());
        }
        catch (DomainException ex)
        {
            return ServiceResult<LoadBandConfigurationDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(UpdateLoadBandConfigurationDto.Bands)] = [ex.Message]
            });
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<LoadBandConfigurationDto>.Success(ToDto(config));
    }

    // Singleton get-or-seed: the migration seeds the default row, but this keeps
    // GET/PUT robust against a missing row (fresh/test databases).
    private async Task<LoadBandConfiguration> GetOrSeedAsync(CancellationToken ct)
    {
        var config = await repository.GetByIdAsync(LoadBandConfiguration.SingletonId, ct);
        if (config is null)
        {
            config = LoadBandConfiguration.CreateDefault();
            await repository.AddAsync(config, ct);
            await repository.SaveChangesAsync(ct);
        }
        return config;
    }

    private static LoadBandConfigurationDto ToDto(LoadBandConfiguration config) => new()
    {
        Bands = config.Bands
            .Select(b => new LoadBandDto { Label = b.Label, LowerBound = b.LowerBound })
            .ToList()
    };
}
