using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

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
    // Exposes the get-or-seeded aggregate to the triage detector, which needs the
    // aggregate's own derivations (OverloadFloor / HealthyFloor) rather than the
    // DTO's raw band list.
    public Task<LoadBandConfiguration> GetConfigurationAsync(CancellationToken ct = default) =>
        GetOrSeedAsync(ct);

    private Task<LoadBandConfiguration> GetOrSeedAsync(CancellationToken ct) =>
        SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.LoadBandConfigurations.FirstOrDefaultAsync(token),
            LoadBandConfiguration.CreateDefault,
            ct);

    private static LoadBandConfigurationDto ToDto(LoadBandConfiguration config) => new()
    {
        Bands = config.Bands
            .Select(b => new LoadBandDto { Label = b.Label, LowerBound = b.LowerBound })
            .ToList()
    };
}
