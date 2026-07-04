namespace ResourcePulse.Domain.Events;

// Raised by the coverage factory (Allocation.CreateCoverage). Carries the demand
// it covers (Phase 5.1, ADR-0025) alongside the resource and denormalized node.
public sealed record AllocationCreated(
    Guid AllocationId,
    Guid DemandId,
    Guid ResourceId,
    Guid ProjectNodeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal AllocationPercent,
    DateTimeOffset OccurredAt) : IDomainEvent;
