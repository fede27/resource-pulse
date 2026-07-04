using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Allocations;

// READ DTOs only. The write-shaped DTOs (create/move/split/…) and their
// validators are retired in favour of the plan command union (ADR-0018) —
// see ResourcePulse.Services.Plan.

public sealed class AllocationReadDto
{
    public Guid Id { get; init; }

    // Coverage always has a resource and covers a demand (Phase 5.1, ADR-0025).
    public Guid ResourceId { get; init; }
    public string? ResourceName { get; init; }

    // The covering person's OWN role (Resource.RoleId). Shown beside DemandRole*
    // so the role match/mismatch is visible (§6): "covering <demand role> as
    // <person role>". Null for a resource without a role.
    public Guid? ResourceRoleId { get; init; }
    public string? ResourceRoleName { get; init; }

    // The demand this coverage covers, and the role the demand ASKS for (§6).
    public Guid DemandId { get; init; }
    public Guid DemandRoleId { get; init; }
    public string DemandRoleName { get; init; } = string.Empty;

    public Guid ProjectNodeId { get; init; }
    public string ProjectNodePath { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }

    // Impegno percepito del blocco (ADR-0015).
    public AllocationStatus Status { get; init; }

    // Hours equivalent at the resource's *current* capacity in the allocation
    // window (ADR-0026). Populated by detail reads; null on list reads (N+1
    // avoidance, ADR-0013, D1).
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
