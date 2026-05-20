namespace ResourcePulse.Services.Allocations;

public sealed class AllocationReadDto
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public Guid ProjectNodeId { get; init; }
    public string ProjectNodePath { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }

    // Hours equivalent at the resource's *current* capacity in the allocation
    // window. Populated by detail/create/update/move endpoints; null on list
    // reads to avoid N+1 capacity queries (mirrors ProjectNodeReadDto's
    // derived metrics — see ADR-0013, D1).
    public TimeSpan? ResolvedHours { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

// Rate-shaped creation: "this resource at X% for this window".
public sealed class CreateByPercentDto
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Percent { get; init; }
    public string? Notes { get; init; }
}

// Quantity-shaped creation: "this resource gets Y hours of work for this window".
// Service resolves to a percent via AllocationResolver using the resource's
// total capacity in the window.
public sealed class CreateByHoursDto
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public TimeSpan TargetHours { get; init; }
    public string? Notes { get; init; }
}

// In-place edit (no period change). Only mutates percent and notes.
public sealed class UpdateAllocationDto
{
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public string? Notes { get; init; }
}

public enum MoveMode
{
    KeepPercent = 0,
    KeepHours
}

// Move the allocation to a new window. The user picks which dimension to
// preserve: the rate (percent stays, hours float with new capacity) or the
// quantity (hours stay, percent floats with new capacity).
public sealed class MoveAllocationDto
{
    public DateOnly NewPeriodStart { get; init; }
    public DateOnly NewPeriodEnd { get; init; }
    public MoveMode Mode { get; init; }
}

// Sidecar payload for GET /api/allocations/{id}/resolved-hours — exposes
// the same data the AllocationReadDto.ResolvedHours field carries on detail
// reads, but in a cheap per-row endpoint for UI badges.
public sealed class AllocationResolvedHoursDto
{
    public decimal AllocationPercent { get; init; }
    public TimeSpan ResolvedHours { get; init; }
    public TimeSpan CapacityInWindow { get; init; }
}
