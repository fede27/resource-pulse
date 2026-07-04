namespace ResourcePulse.Domain.Events;

// Raised on Demand.ChangeOwner when the owner actually changes. Either side may
// be null (an owner can be assigned or cleared).
public sealed record DemandOwnerChanged(
    Guid DemandId,
    Guid? OldOwnerResourceId,
    Guid? NewOwnerResourceId,
    DateTimeOffset OccurredAt) : IDomainEvent;
