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
