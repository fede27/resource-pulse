using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Services.Configuration;

// ── Load bands (ADR-0020 §1) ────────────────────────────────────────────────

public sealed class LoadBandDto
{
    public string Label { get; init; } = string.Empty;
    public decimal LowerBound { get; init; }
}

public sealed class LoadBandConfigurationDto
{
    public IReadOnlyList<LoadBandDto> Bands { get; init; } = [];
}

public sealed class UpdateLoadBandConfigurationDto
{
    public List<LoadBandDto> Bands { get; init; } = [];
}

// ── Time fence (ADR-0020 §2) ────────────────────────────────────────────────

public sealed class DurationDto
{
    public int Value { get; init; }
    public DurationUnit Unit { get; init; }
}

public sealed class TimeFenceConfigurationDto
{
    public DurationDto FrozenHorizon { get; init; } = new();
    public DurationDto SlushyHorizon { get; init; } = new();
}

public sealed class UpdateTimeFenceConfigurationDto
{
    public DurationDto FrozenHorizon { get; init; } = new();
    public DurationDto SlushyHorizon { get; init; } = new();
}

// ── Bucketing (ADR-0020 §3) ─────────────────────────────────────────────────

public sealed class BucketingDefaultsDto
{
    public BucketGrain PrimaryGrain { get; init; }
    public BucketGrain SecondaryGrain { get; init; }
}

public sealed class UpdateBucketingDefaultsDto
{
    public BucketGrain PrimaryGrain { get; init; }
    public BucketGrain SecondaryGrain { get; init; }
}

// ── Commitment policy (ADR-0020 §4) ─────────────────────────────────────────

public sealed class CommitmentPolicyDto
{
    public IReadOnlyList<CommitmentLevel> HardCommitLevels { get; init; } = [];
}

public sealed class UpdateCommitmentPolicyDto
{
    public List<CommitmentLevel> HardCommitLevels { get; init; } = [];
}
