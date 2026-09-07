using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Org-level singleton (ADR-0020). The ordered ladder of load bands the future
// load read-model (Step 3) resolves a load% into. CONFIGURABLE: number, identity
// and labels of the bands — an org may want 3 or 5. CONSTANT (not config): the
// half-open [lower, nextLower) convention, and that the engine reasons by band,
// not by raw figure (§1 spiegabilità, §6 fasce).
public sealed class LoadBandConfiguration : Entity<Guid>, IAuditable
{
    // One row per tenant, enforced by a unique index on tenant_id (ADR-0029).
    // There is deliberately no well-known singleton id any more: under
    // multi-tenancy a fixed id would collide across tenants.

    private readonly List<LoadBand> _bands = new();

    // Always returned in ascending LowerBound order regardless of load order.
    public IReadOnlyList<LoadBand> Bands =>
        _bands.OrderBy(b => b.LowerBound).ToList();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private LoadBandConfiguration() { }

    // Opinionated default: Under(0) · Healthy(85) · Full(100) · Overloaded(110).
    public static LoadBandConfiguration CreateDefault() =>
        Create(Guid.NewGuid(),
        [
            ("Under", 0m),
            ("Healthy", 85m),
            ("Full", 100m),
            ("Overloaded", 110m)
        ]);

    public static LoadBandConfiguration Create(Guid id, IReadOnlyList<(string Label, decimal LowerBound)> bands)
    {
        var config = new LoadBandConfiguration { Id = id };
        config.SetBands(bands);
        return config;
    }

    public void Replace(IReadOnlyList<(string Label, decimal LowerBound)> bands) => SetBands(bands);

    private void SetBands(IReadOnlyList<(string Label, decimal LowerBound)> bands)
    {
        if (bands is null || bands.Count == 0)
            throw new DomainException("LoadBandConfiguration must define at least one band.");

        // The first band starts at 0; bounds strictly increase ⇒ no gaps, no overlap.
        if (bands[0].LowerBound != 0m)
            throw new DomainException("The first load band must start at lower bound 0.");

        for (var i = 1; i < bands.Count; i++)
            if (bands[i].LowerBound <= bands[i - 1].LowerBound)
                throw new DomainException(
                    "Load band lower bounds must be strictly increasing (no gaps or overlaps).");

        var rebuilt = bands.Select(b => LoadBand.Create(b.Label, b.LowerBound)).ToList();
        _bands.Clear();
        _bands.AddRange(rebuilt);
    }

    // Half-open resolution: the band is the last one whose LowerBound <= loadPercent.
    // Since the first band starts at 0, any non-negative load resolves; the last
    // band is open-ended toward +∞.
    public LoadBand Resolve(decimal loadPercent)
    {
        var ordered = Bands;
        LoadBand? match = null;
        foreach (var band in ordered)
        {
            if (band.LowerBound <= loadPercent) match = band;
            else break;
        }

        return match ?? throw new DomainException(
            $"Load percent {loadPercent} is below the first band's lower bound.");
    }

    // The LAST band is open-ended toward +∞, so its lower bound IS the overload
    // threshold. Named here rather than re-derived at each call site: the triage
    // detector and the boards must not disagree about where overload starts, and
    // ADR-0032 §12 forbids a second copy of the threshold in SignalPolicy.
    public decimal OverloadFloor => Bands[^1].LowerBound;

    // Where "healthy" begins — the SECOND band's lower bound, since the first is
    // the under-loaded one starting at 0. Below it and above zero is slack; at
    // exactly zero there is no data at all, which is a different thing and the
    // caller's job to exclude (ADR-0032 §7).
    //
    // A single-band configuration has no such boundary: nothing is under-loaded
    // when there is only one band, so the floor is 0 and the slack set is empty.
    public decimal HealthyFloor
    {
        get
        {
            var ordered = Bands;
            return ordered.Count < 2 ? 0m : ordered[1].LowerBound;
        }
    }
}
