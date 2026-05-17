namespace ResourcePulse.Services.Shared;

public sealed class WorkWindowDto
{
    public Guid Id { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
}
