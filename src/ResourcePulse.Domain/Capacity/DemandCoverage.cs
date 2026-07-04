namespace ResourcePulse.Domain.Capacity;

// The reconciliation of one demand against its coverage over a range (Phase 5.2,
// ADR-0025/0026). Hours are the reconciliation truth: CoveredHours = Σ (% ×
// capacity) over the demand's coverage in the range.
//
//   RequiredHours — the demand's target (scalar). Null ⇒ best-effort (revision §7).
//   CoveredHours  — resolved hours actually covering the demand in the range.
//   GapHours      — RequiredHours − CoveredHours, or NULL when RequiredHours is
//                   null (best-effort has no defined gap). A NEGATIVE gap is
//                   over-coverage (surplus) — surfaced, not clamped.
//
// Scalar, not time-positioned (Decision 4): the gap is "N hours uncovered in the
// queried range", not "uncovered from March to May".
public readonly record struct DemandCoverage(
    Guid DemandId,
    Guid ProjectNodeId,
    Guid RoleId,
    TimeSpan? RequiredHours,
    TimeSpan CoveredHours,
    TimeSpan? GapHours);
