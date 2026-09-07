namespace ResourcePulse.Domain.Signals;

// The catalogue of triage-able conditions (ADR-0032 §2). Persisted as a string
// (≤ 20 chars, EF convention), so the numbering is documentation, not storage:
// the decades group by tier, which is itself a pure function of the kind
// (SignalKinds.TierOf) and is deliberately NOT stored.
//
// Adding a kind is a product decision, not a detail: each one must be able to
// carry a VERB and a deep-link, or the dashboard's admission rule is broken.
public enum SignalKind
{
    // ── Breach (per subject: a violation has a name) ─────────────────────
    // Uncovered demand. Keyed on the DEMAND, not on (node, role): multiple
    // demand lines per role on the same node are legal (ADR-0025), so
    // (node, role) is not a key.
    Gap = 1,

    // A tentative coverage whose window overlaps the frozen zone.
    TentativeInFrozen = 2,

    // A resource whose committed peak crosses the overload floor. Keyed on the
    // RESOURCE, not on (resource, week): otherwise a peak sliding by seven days
    // reads as "Resolved + New" instead of "Worsened".
    Overcommit = 3,

    // ── Hygiene (aggregated: one row per kind, N occurrences inside) ─────
    // Demand still open on a Closed/Cancelled root. I4 forbids covering it and
    // demands/coverage excludes those roots — the data exists and is otherwise
    // deliberately invisible.
    DemandOnClosedRoot = 10,

    // Coverage whose window falls outside its node's planned window. Nothing
    // constrains the two, so they drift.
    CoverageOutOfWindow = 11,

    // Demand on a node with no PlannedStart: no deadline is derivable
    // (ADR-0033 §9), so the demand cannot be placed in a fence zone at all.
    DemandOnUndatedNode = 12,

    // No business calendar is marked as default: new resources have no starting
    // pattern.
    NoDefaultCalendar = 13,

    // A deactivated resource still carrying future coverage. I3 is evaluated at
    // creation only, so deactivating someone afterwards leaves coverage behind
    // that keeps counting hours nobody will work.
    InactiveWithCoverage = 14,

    // ── Slack (aggregated) ──────────────────────────────────────────────
    // People below the healthy band. Aggregated on purpose: never a per-person
    // row, never a magnitude derived from a utilization rate (ADR-0032 §7).
    UnderBand = 20
}
