using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Signals;

// Pure classification of a SignalKind — the domain's single source for "which
// tier is this" and "is this per-subject or aggregated" (ADR-0032 §7, §11).
//
// Both are FUNCTIONS OF THE KIND and are deliberately not stored, same treatment
// as ProjectNodeReadDto.IsProposed (ADR-0021 M3): a stored copy is a second
// source that can drift from the enum it mirrors.
public static class SignalKinds
{
    public static SignalTier TierOf(SignalKind kind) => kind switch
    {
        SignalKind.Gap or
        SignalKind.TentativeInFrozen or
        SignalKind.Overcommit => SignalTier.Breach,

        SignalKind.DemandOnClosedRoot or
        SignalKind.CoverageOutOfWindow or
        SignalKind.DemandOnUndatedNode or
        SignalKind.NoDefaultCalendar or
        SignalKind.InactiveWithCoverage => SignalTier.Hygiene,

        SignalKind.UnderBand => SignalTier.Slack,

        _ => throw new DomainException($"Unclassified signal kind '{kind}'.")
    };

    // Breaches are named individually; hygiene and slack are aggregated. The two
    // classifications coincide by design — the tier IS the line (ADR-0032 §7).
    public static SignalShape ShapeOf(SignalKind kind) =>
        TierOf(kind) == SignalTier.Breach ? SignalShape.Subject : SignalShape.Aggregate;

    public static bool RequiresSubject(SignalKind kind) => ShapeOf(kind) == SignalShape.Subject;

    public static void AssertKnown(SignalKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new DomainException($"Invalid signal kind '{kind}'.");
        _ = TierOf(kind); // throws if the kind is defined but unclassified
    }
}
