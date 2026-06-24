using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Configuration;

public sealed class BucketingDefaultsService(
    IRepository<BucketingDefaults, Guid> repository) : IBucketingDefaultsService
{
    public async Task<ServiceResult<BucketingDefaultsDto>> GetAsync(CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);
        return ServiceResult<BucketingDefaultsDto>.Success(ToDto(config));
    }

    public async Task<ServiceResult<BucketingDefaultsDto>> UpdateAsync(
        UpdateBucketingDefaultsDto dto, CancellationToken ct = default)
    {
        var config = await GetOrSeedAsync(ct);

        try
        {
            config.Replace(dto.PrimaryGrain, dto.SecondaryGrain);
        }
        catch (DomainException ex)
        {
            return ServiceResult<BucketingDefaultsDto>.Validation(new Dictionary<string, string[]>
            {
                ["Bucketing"] = [ex.Message]
            });
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<BucketingDefaultsDto>.Success(ToDto(config));
    }

    private async Task<BucketingDefaults> GetOrSeedAsync(CancellationToken ct)
    {
        var config = await repository.GetByIdAsync(BucketingDefaults.SingletonId, ct);
        if (config is null)
        {
            config = BucketingDefaults.CreateDefault();
            await repository.AddAsync(config, ct);
            await repository.SaveChangesAsync(ct);
        }
        return config;
    }

    private static BucketingDefaultsDto ToDto(BucketingDefaults config) => new()
    {
        PrimaryGrain = config.PrimaryGrain,
        SecondaryGrain = config.SecondaryGrain
    };
}
