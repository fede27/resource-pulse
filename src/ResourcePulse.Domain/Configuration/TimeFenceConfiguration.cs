using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Computed zone boundaries for a given "today". Dates, not durations — these are
// the rolling projection of the stored horizons onto a concrete day.
public sealed record FenceBoundaries(DateOnly FrozenUntil, DateOnly SlushyUntil);

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
    public static readonly Guid SingletonId = new("a1b1c1d1-0000-0000-0000-000000000002");

    public Duration FrozenHorizon { get; private set; } = null!;
    public Duration SlushyHorizon { get; private set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private TimeFenceConfiguration() { }

    // Opinionated default: frozen = 2 weeks, slushy = 2 months.
    public static TimeFenceConfiguration CreateDefault() =>
        Create(SingletonId, Duration.Of(2, DurationUnit.Weeks), Duration.Of(2, DurationUnit.Months));

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

        FrozenHorizon = frozenHorizon;
        SlushyHorizon = slushyHorizon;
    }

    // Rolling projection: recomputed against the supplied "today".
    public FenceBoundaries ComputeBoundaries(DateOnly today) =>
        new(FrozenHorizon.AddTo(today), SlushyHorizon.AddTo(today));

    // Classifies a date relative to "today". Frozen takes precedence at the
    // boundary (inclusive), then Slushy, then Liquid.
    public FenceZone ZoneFor(DateOnly date, DateOnly today)
    {
        var b = ComputeBoundaries(today);
        if (date <= b.FrozenUntil) return FenceZone.Frozen;
        if (date <= b.SlushyUntil) return FenceZone.Slushy;
        return FenceZone.Liquid;
    }
}
