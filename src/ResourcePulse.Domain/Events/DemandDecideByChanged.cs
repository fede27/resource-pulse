namespace ResourcePulse.Domain.Events;

// Raised on Demand.ChangeDecideBy when the explicit decision deadline actually
// changes (ADR-0033 §3). Worth its own event because it is an OVERRIDE: the
// normal case is the derived deadline, so somebody setting one is stating that
// this demand needs deciding on a different schedule than the org default —
// exactly the kind of intent the provenance record exists for.
public sealed record DemandDecideByChanged(
    Guid DemandId,
    DateOnly? OldDecideBy,
    DateOnly? NewDecideBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
