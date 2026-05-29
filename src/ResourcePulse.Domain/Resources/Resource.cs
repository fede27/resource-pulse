using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Events;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Domain.Resources;

public sealed class Resource : Entity<Guid>, IAuditable
{
    private readonly List<WorkWindow> _workWindows = new();
    private readonly List<IndividualAdjustment> _adjustments = new();
    private readonly List<ResourceSkill> _skills = new();
    private readonly List<ResourceTag> _tags = new();

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Guid BusinessCalendarId { get; private set; }
    public Guid? TeamId { get; private set; }

    // Optional link to an auth subject (ClaimTypes.NameIdentifier). When set,
    // ICurrentUserAccessor.User.Sub can be resolved to this resource — used
    // e.g. to attribute skill-approval reviews to the acting supervisor.
    public string? UserSub { get; private set; }

    public IReadOnlyCollection<WorkWindow> WorkWindows => _workWindows.AsReadOnly();
    public IReadOnlyCollection<IndividualAdjustment> Adjustments => _adjustments.AsReadOnly();
    public IReadOnlyCollection<ResourceSkill> Skills => _skills.AsReadOnly();
    public IReadOnlyCollection<ResourceTag> Tags => _tags.AsReadOnly();

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

    public void LinkToUser(string? userSub)
    {
        if (userSub is null)
        {
            UserSub = null;
            return;
        }
        var trimmed = userSub.Trim();
        UserSub = trimmed.Length == 0 ? null : trimmed;
    }

    // ── Team ────────────────────────────────────────────────────────────────

    public void AssignToTeam(Guid? teamId)
    {
        // Treat Guid.Empty as null for ergonomics on the wire.
        var newTeamId = teamId == Guid.Empty ? (Guid?)null : teamId;
        if (newTeamId == TeamId) return;

        var oldTeamId = TeamId;
        TeamId = newTeamId;
        RaiseEvent(new ResourceTeamChanged(Id, oldTeamId, newTeamId, DateTimeOffset.UtcNow));
    }

    // ── Skills ──────────────────────────────────────────────────────────────

    public void AddSkill(Guid skillId, SkillLevel level)
    {
        if (_skills.Any(s => s.SkillId == skillId))
            throw new DomainException($"Resource already has skill {skillId}; use UpdateSkillLevel.");
        _skills.Add(ResourceSkill.Create(Id, skillId, level));
    }

    public void UpdateSkillLevel(Guid skillId, SkillLevel level)
    {
        var existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"Resource does not have skill {skillId}.");
        existing.SetLevel(level);
    }

    public void RemoveSkill(Guid skillId)
    {
        var existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"Resource does not have skill {skillId}.");
        _skills.Remove(existing);
    }

    public void ApproveSkill(Guid skillId, Guid reviewerResourceId, DateTime reviewedAt)
    {
        var existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"Resource does not have skill {skillId}.");
        existing.Approve(reviewerResourceId, reviewedAt);
    }

    public void RejectSkill(Guid skillId, Guid reviewerResourceId, DateTime reviewedAt)
    {
        var existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"Resource does not have skill {skillId}.");
        existing.Reject(reviewerResourceId, reviewedAt);
    }

    public void ReturnSkillToPending(Guid skillId)
    {
        var existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"Resource does not have skill {skillId}.");
        existing.ReturnToPending();
    }

    // ── Tags ────────────────────────────────────────────────────────────────

    public void AddTag(Guid tagId)
    {
        if (_tags.Any(t => t.TagId == tagId))
            throw new DomainException($"Resource already has tag {tagId}.");
        _tags.Add(ResourceTag.Create(Id, tagId));
    }

    public void RemoveTag(Guid tagId)
    {
        var existing = _tags.FirstOrDefault(t => t.TagId == tagId);
        if (existing is null)
            throw new DomainException($"Resource does not have tag {tagId}.");
        _tags.Remove(existing);
    }

    // ── Work windows / adjustments (unchanged) ──────────────────────────────

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
