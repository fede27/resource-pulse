using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Allocations;

// READ DTOs only. The write-shaped DTOs (create/move/split/…) and their
// validators are retired in favour of the plan command union (ADR-0018) —
// see ResourcePulse.Services.Plan.

public sealed class AllocationReadDto
{
    public Guid Id { get; init; }

    // ResourceId is null for placeholder allocations (ADR-0016). When null,
    // the placeholder fields below are populated; when set, they are null.
    public Guid? ResourceId { get; init; }
    public string? ResourceName { get; init; }

    public Guid ProjectNodeId { get; init; }
    public string ProjectNodePath { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }

    // Impegno percepito del blocco (ADR-0015).
    public AllocationStatus Status { get; init; }

    // Placeholder fields — valorizzati iff ResourceId is null (ADR-0016).
    public Guid? RoleSkillId { get; init; }
    public string? RoleSkillName { get; init; }
    public Guid? OwnerResourceId { get; init; }
    public string? OwnerResourceName { get; init; }
    public bool IsPlaceholder => ResourceId is null;

    // Hours equivalent at the resource's *current* capacity in the allocation
    // window. Populated by detail reads; null on list reads (N+1 avoidance,
    // ADR-0013, D1) and for placeholders (no resource ⇒ no capacity).
    public TimeSpan? ResolvedHours { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

// Sidecar payload for GET /api/allocations/{id}/resolved-hours — exposes the
// same data the detail read carries, in a cheap per-row endpoint for UI badges.
public sealed class AllocationResolvedHoursDto
{
    public decimal AllocationPercent { get; init; }
    public TimeSpan ResolvedHours { get; init; }
    public TimeSpan CapacityInWindow { get; init; }
}
