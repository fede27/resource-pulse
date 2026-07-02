namespace ResourcePulse.Domain.Capacity;

// One run-length segment of a resource's commitment profile (gap #4+#10).
//
// A segment is a maximal run of consecutive dates over which the resource's
// committed rate% — and its decomposition by project — does not change. Adjacent
// days with an identical (Percent, ByProject) shape collapse into one segment, so
// the profile is a compact contiguous cover of the requested horizon rather than a
// day-by-day series.
//
//   From / To   — inclusive date bounds of the run.
//   Percent     — the resource's total committed rate% over the run = sum of the
//                 rate% of its active ASSIGNED allocations (placeholders excluded,
//                 ADR-0016 §5). Capacity-independent: for a single resource on a
//                 working day this equals the capacity-normalised LoadPercent, but
//                 it carries no zero-capacity sentinel and needs no capacity input.
//                 Overcommitment (> 100%) is first-class (ADR-0013).
//   ByProject   — Percent decomposed by ROOT project node id; the values sum to
//                 Percent exactly. Keyed by root so "which projects make up this
//                 person's load" lines up with the project rows.
//
// The peak is max(Percent) over the segments — a trivial caller-side derivation,
// not a bespoke field (project-gap.md §★★). Load bands/thresholds stay in
// GET /api/config/load-bands: this profile knows nothing about bands or labels.
public readonly record struct LoadSegment(
    DateOnly From,
    DateOnly To,
    decimal Percent,
    IReadOnlyDictionary<Guid, decimal> ByProject);
