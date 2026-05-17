using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Calendars;

// Owned value object — appears on both BusinessCalendar and Resource.
// Validity period is half-open: [ValidFrom, ValidTo). Open-ended when ValidTo is null.
public sealed class WorkWindow
{
    public Guid Id { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    public TimeSpan Duration => EndTime - StartTime;

    private WorkWindow() { }

    public static WorkWindow Create(
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo)
    {
        if (startTime >= endTime)
            throw new DomainException("WorkWindow start time must be earlier than end time.");
        if (validTo is not null && validTo <= validFrom)
            throw new DomainException("WorkWindow ValidTo must be later than ValidFrom (half-open interval).");

        return new WorkWindow
        {
            Id = Guid.NewGuid(),
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
    }

    public bool IsActiveOn(DateOnly date) =>
        ValidFrom <= date && (ValidTo is null || date < ValidTo);

    public bool AppliesTo(DateOnly date) =>
        IsActiveOn(date) && DayOfWeek == date.DayOfWeek;

    // Validity-period overlap for the same DayOfWeek (half-open [ValidFrom, ValidTo)).
    internal bool ValidityOverlaps(WorkWindow other)
    {
        var thisEnd = ValidTo ?? DateOnly.MaxValue;
        var otherEnd = other.ValidTo ?? DateOnly.MaxValue;
        return ValidFrom < otherEnd && other.ValidFrom < thisEnd;
    }

    // Half-open time-of-day overlap [StartTime, EndTime).
    internal bool TimeOfDayOverlaps(WorkWindow other) =>
        StartTime < other.EndTime && other.StartTime < EndTime;
}
