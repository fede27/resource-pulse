using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Owned value object of LoadBandConfiguration. A single band is a label plus the
// inclusive lower bound (in load %) at which it starts. The band's *upper* bound
// is the next band's LowerBound (exclusive) — the half-open convention lives on
// the configuration, not here. The last band is open-ended toward +∞.
public sealed class LoadBand
{
    public string Label { get; private set; } = string.Empty;
    public decimal LowerBound { get; private set; }

    private LoadBand() { }

    public static LoadBand Create(string label, decimal lowerBound)
    {
        var trimmed = (label ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("LoadBand label must not be empty.");
        if (lowerBound < 0)
            throw new DomainException("LoadBand lower bound must not be negative.");

        return new LoadBand { Label = trimmed, LowerBound = lowerBound };
    }
}
