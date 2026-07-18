using ResourcePulse.Domain.Demands;

namespace ResourcePulse.Services.Demands;

// READ DTO for the Demand aggregate (Phase 5.0). Write goes through the plan
// command envelope (createDemand/editDemand/deleteDemand — ADR-0018), not a CRUD
// controller.
public sealed class DemandReadDto
{
    public Guid Id { get; init; }

    public Guid ProjectNodeId { get; init; }
    public string ProjectNodePath { get; init; } = string.Empty;

    // The required role (§6). Resolved to a name so the page shows the role
    // without an extra round-trip (ADR-0024 pattern).
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;

    // Null => best-effort: the demand exists and can be covered, but there is no
    // target and therefore no defined gap (revision §7).
    public TimeSpan? RequiredHours { get; init; }
    public bool IsBestEffort => RequiredHours is null;

    public DemandProvenance Provenance { get; init; }

    public Guid? OwnerResourceId { get; init; }
    public string? OwnerResourceName { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

// An open (not fully covered) demand across the whole plan (ADR-0027). Same
// reconciliation as DemandCoverageDto, restricted to GapHours > 0 or best-effort,
// enriched with the ROOT project so a cross-project picker needs no extra
// round-trip (ADR-0024 pattern). Residual stays scalar over the queried range
// (Decision 4) — never a reconstructed span.
public sealed class OpenDemandDto
{
    public Guid DemandId { get; init; }

    public Guid ProjectNodeId { get; init; }
    public Guid RootProjectId { get; init; }
    public string RootProjectName { get; init; } = string.Empty;

    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public DemandProvenance Provenance { get; init; }

    public TimeSpan? RequiredHours { get; init; }
    public TimeSpan CoveredHours { get; init; }
    // Null ⇒ best-effort: the demand can absorb coverage but has no defined gap.
    public TimeSpan? GapHours { get; init; }
    public bool IsBestEffort => RequiredHours is null;

    public Guid? OwnerResourceId { get; init; }
    public string? OwnerResourceName { get; init; }

    public string? Notes { get; init; }
}

// Demand-vs-coverage reconciliation over a range (Phase 5.2, ADR-0025/0026). The
// gap read model: RequiredHours (target, nullable), CoveredHours (resolved hours),
// GapHours (required − covered, null for best-effort; negative = surplus). Scalar
// over the queried range (Decision 4) — not time-positioned.
public sealed class DemandCoverageDto
{
    public Guid DemandId { get; init; }
    public Guid ProjectNodeId { get; init; }

    // Root project of the demand's node (first segment of the materialized
    // Path), batch-resolved in the reconciliation (ADR-0024 pattern,
    // consolidation P4) — the cross-project read pivots on it client-side.
    public Guid RootProjectId { get; init; }
    public string RootProjectName { get; init; } = string.Empty;

    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public DemandProvenance Provenance { get; init; }

    public TimeSpan? RequiredHours { get; init; }
    public TimeSpan CoveredHours { get; init; }
    // Null ⇒ best-effort (no target, no defined gap). Negative ⇒ over-coverage.
    public TimeSpan? GapHours { get; init; }
    public bool IsBestEffort => RequiredHours is null;

    public Guid? OwnerResourceId { get; init; }
    public string? OwnerResourceName { get; init; }
}
