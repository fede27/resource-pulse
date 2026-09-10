using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Computed zone boundaries for a given "today". Dates, not durations — these are
// the rolling projection of the stored horizons onto a concrete day.
public sealed record FenceBoundaries(DateOnly FrozenUntil, DateOnly SlushyUntil)
{
    // Classifies a date against these boundaries. Frozen takes precedence at the
    // boundary (inclusive), then Slushy, then Liquid.
    //
    // Lives here, not on the configuration, so that everything classifying a date
    // against the fence goes through ONE implementation of the half-open rule —
    // the triage zone resolution (ADR-0033) needs it without holding the
    // configuration aggregate.
    public FenceZone ZoneFor(DateOnly date)
    {
        if (date <= FrozenUntil) return FenceZone.Frozen;
        if (date <= SlushyUntil) return FenceZone.Slushy;
        return FenceZone.Liquid;
    }
}

// Org-level singleton (ADR-0020). Two rolling horizons from "today" partition the
// timeline into three zones: Frozen = [today, FrozenUntil], Slushy =
// (FrozenUntil, SlushyUntil], Liquid = everything beyond.
//
// CONFIGURABLE: the two boundaries, each as a value+unit Duration.
// CONSTANT (declared, not config): rolling-from-today (durations stored, zones
// recomputed at each read); the 3-zone trichotomy; the zone→behaviour mapping.
// SCOPE of this step = the BOUNDARIES ONLY. Behaviour modulation lands with the
// disruptive plan operations (their home is the command envelope); see ADR-0020.
public sealed class TimeFenceConfiguration : Entity<Guid>, IAuditable
{
    // The committing horizon is not just a label: the detector MEASURES it, through
    // read models that refuse a range wider than this (DateRangeGuard.MaxDays,
    // SignalDetectionService's ChunkDays — both now ARE this constant). A
    // longer horizon would make every read on it fail, and a detector that cannot
    // look is a dashboard saying "the plan holds" about a plan nobody examined. So
    // the boundary is bounded here, where it is chosen, rather than discovered as
    // an empty sweep months later.
    public const int MaxHorizonDays = 366;

    // One row per tenant, enforced by a unique index on tenant_id (ADR-0029).
    public Duration FrozenHorizon { get; private set; } = null!;
    public Duration SlushyHorizon { get; private set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private TimeFenceConfiguration() { }

    // Opinionated default: frozen = 2 weeks, slushy = 2 months.
    public static TimeFenceConfiguration CreateDefault() =>
        Create(Guid.NewGuid(), Duration.Of(2, DurationUnit.Weeks), Duration.Of(2, DurationUnit.Months));

    public static TimeFenceConfiguration Create(Guid id, Duration frozenHorizon, Duration slushyHorizon)
    {
        var config = new TimeFenceConfiguration { Id = id };
        config.SetHorizons(frozenHorizon, slushyHorizon);
        return config;
    }

    public void Replace(Duration frozenHorizon, Duration slushyHorizon) =>
        SetHorizons(frozenHorizon, slushyHorizon);

    private void SetHorizons(Duration frozenHorizon, Duration slushyHorizon)
    {
        ArgumentNullException.ThrowIfNull(frozenHorizon);
        ArgumentNullException.ThrowIfNull(slushyHorizon);

        // Normalize to a comparable length (months ≈ 30 days) and require strict
        // ordering. Liquid is implicit (everything beyond slushy), so it needs no
        // boundary.
        if (frozenHorizon.ApproximateDays >= slushyHorizon.ApproximateDays)
            throw new DomainException(
                "Frozen horizon must be strictly shorter than the slushy horizon.");

        // Worst-case projection, not the 30-day approximation: the span must fit the
        // read models' cap on EVERY "today", leap Februaries included. Twelve months
        // is therefore not expressible — 365 days or 52 weeks are.
        if (slushyHorizon.LongestProjectedDays + 1 > MaxHorizonDays)
            throw new DomainException(
                $"Slushy horizon must not exceed {MaxHorizonDays} days: beyond it the " +
                "detector cannot read the plan and would report an empty queue.");

        FrozenHorizon = frozenHorizon;
        SlushyHorizon = slushyHorizon;
    }

    // Rolling projection: recomputed against the supplied "today".
    public FenceBoundaries ComputeBoundaries(DateOnly today) =>
        new(FrozenHorizon.AddTo(today), SlushyHorizon.AddTo(today));

    // Classifies a date relative to "today". Delegates the half-open rule to the
    // computed boundaries so there is one implementation of it.
    public FenceZone ZoneFor(DateOnly date, DateOnly today) =>
        ComputeBoundaries(today).ZoneFor(date);
}
