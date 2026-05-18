using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Domain.Projects;

// Owned by ProjectNode (root nodes only — enforced by ProjectNode.AddSkillRequirement).
// Composite key (ProjectNodeId, SkillId). MinLevel is the required minimum competency.
public sealed class ProjectSkillRequirement : IAuditable
{
    public Guid ProjectNodeId { get; private set; }
    public Guid SkillId { get; private set; }
    public SkillLevel MinLevel { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private ProjectSkillRequirement() { }

    internal static ProjectSkillRequirement Create(Guid projectNodeId, Guid skillId, SkillLevel minLevel)
    {
        if (skillId == Guid.Empty)
            throw new DomainException("ProjectSkillRequirement must reference a skill.");
        if (!Enum.IsDefined(minLevel))
            throw new DomainException($"Invalid minimum skill level '{minLevel}'.");

        return new ProjectSkillRequirement
        {
            ProjectNodeId = projectNodeId,
            SkillId = skillId,
            MinLevel = minLevel
        };
    }

    internal void SetMinLevel(SkillLevel minLevel)
    {
        if (!Enum.IsDefined(minLevel))
            throw new DomainException($"Invalid minimum skill level '{minLevel}'.");
        MinLevel = minLevel;
    }
}
