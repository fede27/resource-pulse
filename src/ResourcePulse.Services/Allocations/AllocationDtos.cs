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
    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed class CreateAllocationDto
{
    public Guid ResourceId { get; init; }
    public Guid ProjectNodeId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public string? Notes { get; init; }
}

// ResourceId and ProjectNodeId are not mutable on an existing allocation — to
// move an allocation between resources or nodes, delete and recreate.
public sealed class UpdateAllocationDto
{
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal AllocationPercent { get; init; }
    public string? Notes { get; init; }
}
