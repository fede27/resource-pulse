using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Configuration;

// Direct DbContext injection (per the repository convention) because the
// get-or-seed is a *filtered* single-row load: since ADR-0029 the row is
// identified by the ambient tenant via the global query filter, not by a
// well-known id.
public sealed class LoadBandConfigurationService(
    IRepository<LoadBandConfiguration, Guid> repository,
    ResourcePulseDbContext db) : ILoadBandConfigurationService
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

    // Per-tenant singleton get-or-seed. The tenant query filter reduces the table
    // to this tenant's single row; a missing row (a freshly provisioned tenant)
    // seeds the opinionated default on first read.
    private async Task<LoadBandConfiguration> GetOrSeedAsync(CancellationToken ct)
    {
        var config = await db.LoadBandConfigurations.FirstOrDefaultAsync(ct);
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
