using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Domain.Tests.Builders;

public sealed class BusinessCalendarBuilder
{
    private string _name = "Test calendar";
    private bool _isDefault;
    private readonly List<(DayOfWeek dow, TimeOnly start, TimeOnly end, DateOnly vf, DateOnly? vt)> _windows = new();

    public BusinessCalendarBuilder Named(string name) { _name = name; return this; }
    public BusinessCalendarBuilder Default() { _isDefault = true; return this; }

    public BusinessCalendarBuilder WithWindow(
        DayOfWeek dow,
        string start,
        string end,
        string validFrom = "2020-01-01",
        string? validTo = null)
    {
        _windows.Add((
            dow,
            TimeOnly.Parse(start),
            TimeOnly.Parse(end),
            DateOnly.Parse(validFrom),
            validTo is null ? null : DateOnly.Parse(validTo)));
        return this;
    }

    // Convenience: add the same window on every day in [from..to].
    public BusinessCalendarBuilder WithWindowOnDays(
        IEnumerable<DayOfWeek> days,
        string start,
        string end,
        string validFrom = "2020-01-01",
        string? validTo = null)
    {
        foreach (var d in days)
            WithWindow(d, start, end, validFrom, validTo);
        return this;
    }

    public BusinessCalendar Build()
    {
        var calendar = BusinessCalendar.Create(_name, _isDefault);
        foreach (var (dow, start, end, vf, vt) in _windows)
            calendar.AddWorkWindow(WorkWindow.Create(dow, start, end, vf, vt));
        return calendar;
    }
}
