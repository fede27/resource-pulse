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

    // Ore aggregate da SOLE allocazioni assegnate (ADR-0016 §5). Per i
    // placeholder non c'è capacity di riferimento, quindi nessuna ora.
    public TimeSpan TotalHours { get; init; }
    public IReadOnlyList<NodeLoadByResourceDto> ByResource { get; init; }
        = Array.Empty<NodeLoadByResourceDto>();

    // Somma di rate% sui placeholder attivi nella data — la "domanda scoperta"
    // sul nodo (ADR-0016 §5). 0 quando non ci sono placeholder.
    public decimal PlaceholderRatePercent { get; init; }
}

public sealed class NodeLoadByResourceDto
{
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public TimeSpan Hours { get; init; }
}
