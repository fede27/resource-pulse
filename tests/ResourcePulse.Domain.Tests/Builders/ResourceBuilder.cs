using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Domain.Tests.Builders;

public sealed class ResourceBuilder
{
    private string _name = "Test resource";
    private Guid _calendarId = Guid.NewGuid();
    private readonly List<(DayOfWeek dow, TimeOnly start, TimeOnly end, DateOnly vf, DateOnly? vt)> _overrides = new();
    private readonly List<(DateOnly from, DateOnly to, AdjustmentType type, TimeSpan? hours, string reason)> _adjustments = new();

    public ResourceBuilder Named(string name) { _name = name; return this; }
    public ResourceBuilder OnCalendar(Guid id) { _calendarId = id; return this; }
    public ResourceBuilder OnCalendar(BusinessCalendar calendar) { _calendarId = calendar.Id; return this; }

    public ResourceBuilder OverrideWindow(
        DayOfWeek dow,
        string start,
        string end,
        string validFrom = "2020-01-01",
        string? validTo = null)
    {
        _overrides.Add((
            dow,
            TimeOnly.Parse(start),
            TimeOnly.Parse(end),
            DateOnly.Parse(validFrom),
            validTo is null ? null : DateOnly.Parse(validTo)));
        return this;
    }

    public ResourceBuilder WithFullDayAbsence(string from, string to, string reason = "Vacation") =>
        WithAdjustment(from, to, AdjustmentType.Absence, null, reason);

    public ResourceBuilder WithPartialAbsence(string from, string to, TimeSpan hours, string reason = "Half day") =>
        WithAdjustment(from, to, AdjustmentType.Absence, hours, reason);

    public ResourceBuilder WithExtra(string from, string to, TimeSpan hours, string reason = "Overtime") =>
        WithAdjustment(from, to, AdjustmentType.ExtraTime, hours, reason);

    public ResourceBuilder WithAdjustment(string from, string to, AdjustmentType type, TimeSpan? hours, string reason)
    {
        _adjustments.Add((DateOnly.Parse(from), DateOnly.Parse(to), type, hours, reason));
        return this;
    }

    public Resource Build()
    {
        var resource = Resource.Create(_name, _calendarId);
        foreach (var (dow, start, end, vf, vt) in _overrides)
            resource.AddWorkWindowOverride(WorkWindow.Create(dow, start, end, vf, vt));
        foreach (var (from, to, type, hours, reason) in _adjustments)
            resource.AddAdjustment(IndividualAdjustment.Create(from, to, type, hours, reason, null));
        return resource;
    }
}
