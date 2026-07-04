namespace ResourcePulse.Domain.Events;

// Raised on Demand.ChangeRole when the role actually changes (amendment C2). The
// inferred role is only a seeded suggestion; once the demand exists its role is
// its own and correctable. Valuable for explainability ("the role of this demand
// was corrected from X to Y"). Correcting it never rewrites existing coverage —
// a resulting person/demand role divergence is the visible mismatch (§6).
public sealed record DemandRoleChanged(
    Guid DemandId,
    Guid OldRoleId,
    Guid NewRoleId,
    DateTimeOffset OccurredAt) : IDomainEvent;
