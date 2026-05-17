using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Calendars;

public sealed class BusinessCalendar : Entity<Guid>, IAuditable
{
    private readonly List<WorkWindow> _workWindows = new();

    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    public IReadOnlyCollection<WorkWindow> WorkWindows => _workWindows.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private BusinessCalendar() { }

    public static BusinessCalendar Create(string name, bool isDefault)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("BusinessCalendar name must not be empty.");

        return new BusinessCalendar
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            IsDefault = isDefault
        };
    }

    // Hydration entry point for the capacity query service: bypasses factory invariants
    // because the data already lives in the database (and presumably passed invariants
    // when first persisted). Loads filtered owned collections without re-validating.
    internal static BusinessCalendar Hydrate(Guid id, string name, bool isDefault, IEnumerable<WorkWindow> windows)
    {
        var calendar = new BusinessCalendar
        {
            Id = id,
            Name = name,
            IsDefault = isDefault
        };
        calendar._workWindows.AddRange(windows);
        return calendar;
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("BusinessCalendar name must not be empty.");
        Name = trimmed;
    }

    public void MarkAsDefault() => IsDefault = true;
    public void UnmarkDefault() => IsDefault = false;

    public void AddWorkWindow(WorkWindow window)
    {
        if (window is null) throw new DomainException("WorkWindow must not be null.");
        EnsureNoOverlap(window);
        _workWindows.Add(window);
    }

    public void RemoveWorkWindow(Guid windowId)
    {
        var window = _workWindows.FirstOrDefault(w => w.Id == windowId);
        if (window is null)
            throw new DomainException($"WorkWindow {windowId} not found on this calendar.");
        _workWindows.Remove(window);
    }

    // Sum of durations for windows applicable to the date. Pure helper; calculator
    // uses this to derive the base pattern hours before applying closures/adjustments.
    public TimeSpan DailyCapacity(DateOnly date) =>
        _workWindows
            .Where(w => w.AppliesTo(date))
            .Aggregate(TimeSpan.Zero, (acc, w) => acc + w.Duration);

    private void EnsureNoOverlap(WorkWindow candidate)
    {
        foreach (var existing in _workWindows)
        {
            if (existing.DayOfWeek != candidate.DayOfWeek) continue;
            if (!existing.ValidityOverlaps(candidate)) continue;
            if (!existing.TimeOfDayOverlaps(candidate)) continue;

            // Same day-of-week with overlapping validity AND overlapping time-of-day —
            // hours would be double-counted. (Non-overlapping times like a lunch break are fine.)
            throw new DomainException(
                $"WorkWindow on {candidate.DayOfWeek} overlaps an existing window's time of day.");
        }
    }
}
