using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Domain.Projects;

public sealed class ProjectNode : Entity<Guid>, IAuditable
{
    private readonly List<ProjectSkillRequirement> _skillRequirements = new();
    private readonly List<ProjectNodeTag> _tags = new();

    // ── Tree ────────────────────────────────────────────────────────────────
    public Guid? ParentId { get; private set; }
    public ProjectNodeType NodeType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public int Depth { get; private set; }

    public IReadOnlyCollection<ProjectSkillRequirement> SkillRequirements => _skillRequirements.AsReadOnly();
    public IReadOnlyCollection<ProjectNodeTag> Tags => _tags.AsReadOnly();

    // ── Project-only fields (null on Phase/WorkPackage) ─────────────────────
    public ProjectType? Type { get; private set; }
    public CommitmentLevel? CommitmentLevel { get; private set; }
    public Guid? LeadResourceId { get; private set; }
    public ProjectStatus? Status { get; private set; }

    // ── Dates (active for Project|Phase per capacity-planning level rule) ───
    public DateOnly? BaselineStart { get; private set; }
    public DateOnly? BaselineEnd { get; private set; }
    public DateTimeOffset? BaselinedAt { get; private set; }
    public DateOnly? PlannedStart { get; private set; }
    public DateOnly? PlannedEnd { get; private set; }
    public DateOnly? ActualStart { get; private set; }
    public DateOnly? ActualEnd { get; private set; }

    // ── Audit ───────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private ProjectNode() { }

    // ── Factories ───────────────────────────────────────────────────────────

    public static ProjectNode CreateRoot(
        string name,
        string? code,
        ProjectType type,
        CommitmentLevel commitmentLevel,
        Guid? leadResourceId)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            throw new DomainException("ProjectNode name must not be empty.");

        var trimmedCode = NormalizeOptional(code);
        if (!Enum.IsDefined(type))
            throw new DomainException($"Invalid project type '{type}'.");
        if (!Enum.IsDefined(commitmentLevel))
            throw new DomainException($"Invalid commitment level '{commitmentLevel}'.");

        var id = Guid.NewGuid();
        var node = new ProjectNode
        {
            Id = id,
            ParentId = null,
            NodeType = ProjectNodeType.Project,
            Name = trimmedName,
            Code = trimmedCode,
            Path = "/" + id.ToString("D"),
            Depth = 0,
            Type = type,
            CommitmentLevel = commitmentLevel,
            LeadResourceId = leadResourceId == Guid.Empty ? null : leadResourceId,
            Status = ProjectStatus.Draft
        };

        node.RaiseEvent(new ProjectNodeCreated(id, null, ProjectNodeType.Project, DateTimeOffset.UtcNow));
        return node;
    }

    public static ProjectNode CreateChild(
        ProjectNode parent,
        ProjectNodeType nodeType,
        string name,
        string? code)
    {
        if (parent is null)
            throw new DomainException("CreateChild requires a parent.");
        if (nodeType == ProjectNodeType.Project)
            throw new DomainException("CreateChild cannot create a Project root (use CreateRoot).");
        if (!Enum.IsDefined(nodeType))
            throw new DomainException($"Invalid node type '{nodeType}'.");
        if (!AllowsChild(parent.NodeType, nodeType))
            throw new DomainException(
                $"A {parent.NodeType} cannot contain a {nodeType}.");

        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            throw new DomainException("ProjectNode name must not be empty.");
        var trimmedCode = NormalizeOptional(code);

        var id = Guid.NewGuid();
        var node = new ProjectNode
        {
            Id = id,
            ParentId = parent.Id,
            NodeType = nodeType,
            Name = trimmedName,
            Code = trimmedCode,
            Path = parent.Path + "/" + id.ToString("D"),
            Depth = parent.Depth + 1
        };

        node.RaiseEvent(new ProjectNodeCreated(id, parent.Id, nodeType, DateTimeOffset.UtcNow));
        return node;
    }

    // ── Identity & metadata ─────────────────────────────────────────────────

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("ProjectNode name must not be empty.");
        Name = trimmed;
    }

    public void ChangeCode(string? code) => Code = NormalizeOptional(code);

    // ── Project-only mutators ───────────────────────────────────────────────

    public void ChangeType(ProjectType type)
    {
        AssertIsProject();
        if (!Enum.IsDefined(type))
            throw new DomainException($"Invalid project type '{type}'.");
        Type = type;
    }

    public void ChangeCommitmentLevel(CommitmentLevel level)
    {
        AssertIsProject();
        if (!Enum.IsDefined(level))
            throw new DomainException($"Invalid commitment level '{level}'.");
        CommitmentLevel = level;
    }

    public void AssignLead(Guid? leadResourceId)
    {
        AssertIsProject();
        LeadResourceId = leadResourceId == Guid.Empty ? null : leadResourceId;
    }

    // ── Tree mutations ──────────────────────────────────────────────────────

    public void Reparent(ProjectNode newParent, IReadOnlyCollection<ProjectNode> descendants)
    {
        if (NodeType == ProjectNodeType.Project)
            throw new DomainException("Project root nodes cannot be reparented.");
        if (newParent is null)
            throw new DomainException("Non-Project nodes require a parent.");
        if (descendants is null)
            throw new DomainException("descendants must not be null (pass an empty collection if this node is a leaf).");
        if (newParent.Id == Id)
            throw new DomainException("Cannot reparent under self.");
        if (newParent.Path == Path ||
            newParent.Path.StartsWith(Path + "/", StringComparison.Ordinal))
            throw new DomainException("Cannot reparent under own descendant (would create a cycle).");
        if (!AllowsChild(newParent.NodeType, NodeType))
            throw new DomainException(
                $"A {newParent.NodeType} cannot contain a {NodeType}.");

        // Defensive: every descendant path must start with this node's old path.
        var oldPrefix = Path + "/";
        foreach (var d in descendants)
        {
            if (!d.Path.StartsWith(oldPrefix, StringComparison.Ordinal))
                throw new DomainException(
                    $"Node {d.Id} is not a descendant of this subtree (path mismatch).");
        }

        var oldParentId = ParentId;
        var oldPath = Path;
        var oldDepth = Depth;
        var newPath = newParent.Path + "/" + Id.ToString("D");
        var newDepth = newParent.Depth + 1;
        var depthDelta = newDepth - oldDepth;

        ParentId = newParent.Id;
        Path = newPath;
        Depth = newDepth;

        foreach (var d in descendants)
        {
            // Rewrite each descendant's path by replacing the old prefix with the new one.
            d.Path = newPath + d.Path.Substring(oldPath.Length);
            d.Depth += depthDelta;
        }

        RaiseEvent(new ProjectNodeReparented(Id, oldParentId, newParent.Id, DateTimeOffset.UtcNow));
    }

    public void RecalculatePlannedFromChildren(IReadOnlyCollection<ProjectNode> children)
    {
        AssertCanHaveCapacityArtifacts();
        if (children is null) return;

        DateOnly? minStart = null;
        DateOnly? maxEnd = null;
        foreach (var c in children)
        {
            if (c.PlannedStart is { } s && (minStart is null || s < minStart)) minStart = s;
            if (c.PlannedEnd   is { } e && (maxEnd   is null || e > maxEnd))   maxEnd   = e;
        }

        if (minStart is null && maxEnd is null) return; // no children with planned dates; leave self untouched
        SetPlanned(minStart, maxEnd, raiseEventOnChange: true);
    }

    public void RecalculateBaselineFromChildren(IReadOnlyCollection<ProjectNode> children)
    {
        AssertCanHaveCapacityArtifacts();
        if (children is null) return;

        DateOnly? minStart = null;
        DateOnly? maxEnd = null;
        foreach (var c in children)
        {
            if (c.BaselineStart is { } s && (minStart is null || s < minStart)) minStart = s;
            if (c.BaselineEnd   is { } e && (maxEnd   is null || e > maxEnd))   maxEnd   = e;
        }

        if (minStart is null || maxEnd is null) return; // baseline requires both ends; partial rollup is a no-op
        BaselineCore(minStart.Value, maxEnd.Value, isRebaseline: BaselineStart is not null);
    }

    // ── Dates ───────────────────────────────────────────────────────────────

    public void Baseline(DateOnly start, DateOnly end)
    {
        AssertCanHaveCapacityArtifacts();
        if (BaselineStart is not null)
            throw new DomainException("Project is already baselined; use Rebaseline.");
        if (start > end)
            throw new DomainException("BaselineStart must be on or before BaselineEnd.");

        BaselineCore(start, end, isRebaseline: false);
    }

    public void Rebaseline(DateOnly start, DateOnly end, string reason)
    {
        AssertCanHaveCapacityArtifacts();
        if (BaselineStart is null)
            throw new DomainException("Project has not been baselined yet; use Baseline.");
        if (start > end)
            throw new DomainException("BaselineStart must be on or before BaselineEnd.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Rebaseline requires a reason.");

        BaselineCore(start, end, isRebaseline: true);
    }

    public void Replan(DateOnly? start, DateOnly? end)
    {
        AssertCanHaveCapacityArtifacts();
        if (start is { } s && end is { } e && s > e)
            throw new DomainException("PlannedStart must be on or before PlannedEnd.");

        SetPlanned(start, end, raiseEventOnChange: true);
    }

    public void BackfillActuals(DateOnly? start, DateOnly? end)
    {
        AssertCanHaveCapacityArtifacts();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (start is { } s)
        {
            if (s > today) throw new DomainException("ActualStart cannot be in the future.");
        }
        if (end is { } e)
        {
            if (e > today) throw new DomainException("ActualEnd cannot be in the future.");
            if (start is null)
                throw new DomainException("ActualEnd requires ActualStart to be set.");
            if (start.Value > e)
                throw new DomainException("ActualStart must be on or before ActualEnd.");
        }
        ActualStart = start;
        ActualEnd = end;
    }

    // ── State transitions (Project root only) ───────────────────────────────

    public void Start()
    {
        AssertIsProject();
        if (Status != ProjectStatus.Draft)
            throw new DomainException($"Cannot Start from status {Status} (only from Draft).");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ActualStart = today;
        TransitionStatus(ProjectStatus.Active);
    }

    public void Complete()
    {
        AssertIsProject();
        if (Status != ProjectStatus.Active)
            throw new DomainException($"Cannot Complete from status {Status} (only from Active).");
        if (ActualStart is null)
            throw new DomainException("Cannot Complete: ActualStart is not set.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ActualEnd = today;
        TransitionStatus(ProjectStatus.Closed);
    }

    public void Suspend(string reason)
    {
        AssertIsProject();
        if (Status != ProjectStatus.Active)
            throw new DomainException($"Cannot Suspend from status {Status} (only from Active).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Suspend requires a reason.");
        TransitionStatus(ProjectStatus.OnHold);
    }

    public void Resume()
    {
        AssertIsProject();
        if (Status != ProjectStatus.OnHold)
            throw new DomainException($"Cannot Resume from status {Status} (only from OnHold).");
        TransitionStatus(ProjectStatus.Active);
    }

    public void Cancel(string reason)
    {
        AssertIsProject();
        if (Status == ProjectStatus.Closed || Status == ProjectStatus.Cancelled)
            throw new DomainException($"Cannot Cancel from status {Status}.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Cancel requires a reason.");
        TransitionStatus(ProjectStatus.Cancelled);
    }

    // ── Skill requirements (Project root only) ──────────────────────────────

    public void AddSkillRequirement(Guid skillId, SkillLevel minLevel)
    {
        AssertIsProject();
        if (_skillRequirements.Any(r => r.SkillId == skillId))
            throw new DomainException($"Skill requirement for skill {skillId} already exists; use UpdateSkillRequirementLevel.");
        _skillRequirements.Add(ProjectSkillRequirement.Create(Id, skillId, minLevel));
    }

    public void UpdateSkillRequirementLevel(Guid skillId, SkillLevel minLevel)
    {
        AssertIsProject();
        var existing = _skillRequirements.FirstOrDefault(r => r.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"No skill requirement exists for skill {skillId}.");
        existing.SetMinLevel(minLevel);
    }

    public void RemoveSkillRequirement(Guid skillId)
    {
        AssertIsProject();
        var existing = _skillRequirements.FirstOrDefault(r => r.SkillId == skillId);
        if (existing is null)
            throw new DomainException($"No skill requirement exists for skill {skillId}.");
        _skillRequirements.Remove(existing);
    }

    // ── Tags (any node type) ────────────────────────────────────────────────

    public void AddTag(Guid tagId)
    {
        if (_tags.Any(t => t.TagId == tagId))
            throw new DomainException($"Node already has tag {tagId}.");
        _tags.Add(ProjectNodeTag.Create(Id, tagId));
    }

    public void RemoveTag(Guid tagId)
    {
        var existing = _tags.FirstOrDefault(t => t.TagId == tagId);
        if (existing is null)
            throw new DomainException($"Node does not have tag {tagId}.");
        _tags.Remove(existing);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Single point of enforcement for the capacity-planning level rule.
    // Skill requirements (added in step 4), date methods, and any future
    // capacity-allocation artifacts gate on this.
    internal void AssertCanHaveCapacityArtifacts()
    {
        if (NodeType != ProjectNodeType.Project && NodeType != ProjectNodeType.Phase)
            throw new DomainException(
                $"Capacity-planning artifacts are not allowed on {NodeType} nodes (only Project and Phase).");
    }

    private void AssertIsProject()
    {
        if (NodeType != ProjectNodeType.Project)
            throw new DomainException(
                $"This operation is only allowed on Project root nodes (current node is {NodeType}).");
    }

    private void TransitionStatus(ProjectStatus to)
    {
        var from = Status!.Value;
        Status = to;
        RaiseEvent(new ProjectStatusChanged(Id, from, to, DateTimeOffset.UtcNow));
    }

    private void BaselineCore(DateOnly start, DateOnly end, bool isRebaseline)
    {
        BaselineStart = start;
        BaselineEnd = end;
        BaselinedAt = DateTimeOffset.UtcNow;
        RaiseEvent(new ProjectBaselined(Id, start, end, isRebaseline, DateTimeOffset.UtcNow));
    }

    private void SetPlanned(DateOnly? start, DateOnly? end, bool raiseEventOnChange)
    {
        if (PlannedStart == start && PlannedEnd == end) return; // no-op suppresses event
        PlannedStart = start;
        PlannedEnd = end;
        if (raiseEventOnChange)
            RaiseEvent(new ProjectReplanned(Id, start, end, DateTimeOffset.UtcNow));
    }

    private static bool AllowsChild(ProjectNodeType parent, ProjectNodeType child) =>
        (parent, child) switch
        {
            (ProjectNodeType.Project, ProjectNodeType.Phase)       => true,
            (ProjectNodeType.Project, ProjectNodeType.WorkPackage) => true,
            (ProjectNodeType.Phase,   ProjectNodeType.WorkPackage) => true,
            _ => false
        };

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
