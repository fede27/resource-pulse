namespace ResourcePulse.Services.Load;

public sealed class DailyLoadDto
{
    public DateOnly Date { get; init; }
    public TimeSpan Hours { get; init; }
    // LoadPercent uses decimal.MaxValue as a sentinel for "load exists on a date
    // with zero capacity" (full-overload signal — Phase 5 will consume this).
    public decimal LoadPercent { get; init; }
}

public sealed class DailyNodeLoadDto
{
    public DateOnly Date { get; init; }

    // Coverage hours on the node/subtree (Phase 5.1, ADR-0025). Uncovered demand
    // is no longer a placeholder rate here — it is the demand-coverage read model.
    public TimeSpan TotalHours { get; init; }
    public IReadOnlyList<NodeLoadByResourceDto> ByResource { get; init; }
        = Array.Empty<NodeLoadByResourceDto>();
}

public sealed class NodeLoadByResourceDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public TimeSpan Hours { get; init; }
}

// Resource commitment profile (gap #4+#10 / ADR-0023): a run-length segment view
// of a person's committed rate% over the horizon, decomposed by root project.
// Replaces the need to download the full daily series just to find the peak and
// its composition: peak = max(Percent) across segments; the peak's composition =
// the ByProject of the peak segment. Load bands stay in GET /api/config/load-bands.
public sealed class LoadSegmentDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    // Total committed rate% over the segment (capacity-independent; > 100 means
    // overcommitment, ADR-0013). The ByProject shares sum to this.
    public decimal Percent { get; init; }
    public IReadOnlyList<LoadSegmentProjectDto> ByProject { get; init; }
        = Array.Empty<LoadSegmentProjectDto>();
}

public sealed class LoadSegmentProjectDto
{
    // Root project node of the contributing allocations.
    public Guid ProjectNodeId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public decimal Percent { get; init; }
}
