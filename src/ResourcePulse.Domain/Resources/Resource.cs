using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Domain.Resources;

public sealed class Resource : Entity<Guid>, IAuditable
{
    private readonly List<WorkWindow> _workWindows = new();
    private readonly List<IndividualAdjustment> _adjustments = new();

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Guid BusinessCalendarId { get; private set; }

    public IReadOnlyCollection<WorkWindow> WorkWindows => _workWindows.AsReadOnly();
    public IReadOnlyCollection<IndividualAdjustment> Adjustments => _adjustments.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Resource() { }

    public static Resource Create(string name, Guid businessCalendarId)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Resource name must not be empty.");
        if (businessCalendarId == Guid.Empty)
            throw new DomainException("Resource must reference a business calendar.");

        return new Resource
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            BusinessCalendarId = businessCalendarId,
            IsActive = true
        };
    }

    // Hydration entry point for the capacity query service: see BusinessCalendar.Hydrate.
    internal static Resource Hydrate(
        Guid id,
        string name,
        Guid businessCalendarId,
        IEnumerable<WorkWindow> windows,
        IEnumerable<IndividualAdjustment> adjustments)
    {
        var resource = new Resource
        {
            Id = id,
            Name = name,
            BusinessCalendarId = businessCalendarId,
            IsActive = true
        };
        resource._workWindows.AddRange(windows);
        resource._adjustments.AddRange(adjustments);
        return resource;
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Resource name must not be empty.");
        Name = trimmed;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void ChangeBusinessCalendar(Guid businessCalendarId)
    {
        if (businessCalendarId == Guid.Empty)
            throw new DomainException("Resource must reference a business calendar.");
        BusinessCalendarId = businessCalendarId;
    }

    public void AddWorkWindowOverride(WorkWindow window)
    {
        if (window is null) throw new DomainException("WorkWindow must not be null.");
        EnsureNoOverlap(window);
        _workWindows.Add(window);
    }

    public void RemoveWorkWindowOverride(Guid windowId)
    {
        var window = _workWindows.FirstOrDefault(w => w.Id == windowId);
        if (window is null)
            throw new DomainException($"WorkWindow {windowId} not found on this resource.");
        _workWindows.Remove(window);
    }

    public void ClearOverrides() => _workWindows.Clear();

    public void AddAdjustment(IndividualAdjustment adjustment)
    {
        if (adjustment is null) throw new DomainException("Adjustment must not be null.");
        _adjustments.Add(adjustment);
    }

    public void RemoveAdjustment(Guid adjustmentId)
    {
        var adjustment = _adjustments.FirstOrDefault(a => a.Id == adjustmentId);
        if (adjustment is null)
            throw new DomainException($"Adjustment {adjustmentId} not found on this resource.");
        _adjustments.Remove(adjustment);
    }

    private void EnsureNoOverlap(WorkWindow candidate)
    {
        foreach (var existing in _workWindows)
        {
            if (existing.DayOfWeek != candidate.DayOfWeek) continue;
            if (!existing.ValidityOverlaps(candidate)) continue;
            if (!existing.TimeOfDayOverlaps(candidate)) continue;
            throw new DomainException(
                $"WorkWindow on {candidate.DayOfWeek} overlaps an existing override's time of day.");
        }
    }
}
