namespace ResourcePulse.Domain.Events;

// Raised on Demand.ChangeRequiredHours when the value actually changes. Either
// side may be null: clearing the target moves the demand to best-effort (no
// gap, revision §7); setting it declares a target.
public sealed record DemandRequiredHoursChanged(
    Guid DemandId,
    TimeSpan? OldRequiredHours,
    TimeSpan? NewRequiredHours,
    DateTimeOffset OccurredAt) : IDomainEvent;
