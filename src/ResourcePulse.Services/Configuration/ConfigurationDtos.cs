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

// The fifth org-level singleton (ADR-0032 §12). Exactly two dials — the depth of
// the change feed, and how much notice a staffing decision needs. Everything else
// about triage is a declared constant or already lives in LoadBandConfiguration.
public sealed class SignalPolicyDto
{
    public int ResolvedRetentionDays { get; init; }
    public DurationDto DecisionLeadTime { get; init; } = new();

    // The queue budget, exposed READ-ONLY so the page can say "7 di 40" without
    // hard-coding the number. Deliberately absent from the update DTO: it is a
    // constant, not a per-tenant dial (ADR-0032 §12).
    public int QueueBudget { get; init; }
}

public sealed class UpdateSignalPolicyDto
{
    public int ResolvedRetentionDays { get; init; }
    public DurationDto DecisionLeadTime { get; init; } = new();
}

public sealed class UpdateCommitmentPolicyDto
{
    public List<CommitmentLevel> HardCommitLevels { get; init; } = [];
}
