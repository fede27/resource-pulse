namespace ResourcePulse.Domain.Signals;

// Ranking tier (ADR-0032 §11). Derived from the kind, never stored: within the
// same fence zone, a broken commitment is more actionable than model hygiene, and
// hygiene more than unused capacity. Without a tier, an informational row of
// magnitude zero can outrank a frozen breach.
//
// The enum order IS the rule (same convention as AppRole in ADR-0030): lower
// sorts first.
public enum SignalTier
{
    Breach = 0,
    Hygiene = 1,
    Slack = 2
}
