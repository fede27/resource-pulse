namespace ResourcePulse.Services.Capacity;

public sealed class DailyCapacityDto
{
    public DateOnly Date { get; init; }
    public TimeSpan Hours { get; init; }
}
