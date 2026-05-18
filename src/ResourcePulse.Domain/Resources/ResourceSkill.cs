using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Domain.Resources;

// Owned by Resource; composite key (ResourceId, SkillId). Stores the level
// at which the resource holds the skill. IAuditable captures when the skill
// was recorded for this resource (CreatedAt) and last updated (UpdatedAt).
public sealed class ResourceSkill : IAuditable
{
    public Guid ResourceId { get; private set; }
    public Guid SkillId { get; private set; }
    public SkillLevel Level { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private ResourceSkill() { }

    internal static ResourceSkill Create(Guid resourceId, Guid skillId, SkillLevel level)
    {
        if (skillId == Guid.Empty)
            throw new DomainException("ResourceSkill must reference a skill.");
        if (!Enum.IsDefined(level))
            throw new DomainException($"Invalid skill level '{level}'.");

        return new ResourceSkill
        {
            ResourceId = resourceId,
            SkillId = skillId,
            Level = level
        };
    }

    internal void SetLevel(SkillLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new DomainException($"Invalid skill level '{level}'.");
        Level = level;
    }
}
