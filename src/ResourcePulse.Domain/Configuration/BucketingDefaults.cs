using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Org-level singleton (ADR-0020). Default primary/secondary aggregation grains the
// future load read-model uses when the caller doesn't specify. CONFIGURABLE: which
// grain each defaults to. CONSTANT: the {day, week, month} enum itself.
public sealed class BucketingDefaults : Entity<Guid>, IAuditable
{
    public static readonly Guid SingletonId = new("a1b1c1d1-0000-0000-0000-000000000003");

    public BucketGrain PrimaryGrain { get; private set; }
    public BucketGrain SecondaryGrain { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private BucketingDefaults() { }

    // Opinionated default: primary = week, secondary = month.
    public static BucketingDefaults CreateDefault() =>
        Create(SingletonId, BucketGrain.Week, BucketGrain.Month);

    public static BucketingDefaults Create(Guid id, BucketGrain primaryGrain, BucketGrain secondaryGrain)
    {
        var config = new BucketingDefaults { Id = id };
        config.SetGrains(primaryGrain, secondaryGrain);
        return config;
    }

    public void Replace(BucketGrain primaryGrain, BucketGrain secondaryGrain) =>
        SetGrains(primaryGrain, secondaryGrain);

    private void SetGrains(BucketGrain primaryGrain, BucketGrain secondaryGrain)
    {
        if (!Enum.IsDefined(primaryGrain))
            throw new DomainException($"Invalid primary bucket grain '{primaryGrain}'.");
        if (!Enum.IsDefined(secondaryGrain))
            throw new DomainException($"Invalid secondary bucket grain '{secondaryGrain}'.");

        PrimaryGrain = primaryGrain;
        SecondaryGrain = secondaryGrain;
    }
}
