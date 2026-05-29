using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Domain.Resources;

// Owned by Resource; composite key (ResourceId, SkillId). Stores the level
// at which the resource holds the skill plus its supervisor-approval state.
// IAuditable captures when the skill was recorded for this resource
// (CreatedAt) and last updated (UpdatedAt).
public sealed class ResourceSkill : IAuditable
{
    public Guid ResourceId { get; private set; }
    public Guid SkillId { get; private set; }
    public SkillLevel Level { get; private set; }

    // Approval workflow: a newly-added ResourceSkill starts in Pending.
    // Reviewer fields are populated on Approve/Reject and cleared on
    // ReturnToPending.
    public SkillApprovalStatus ApprovalStatus { get; private set; } = SkillApprovalStatus.Pending;
    public Guid? ReviewedByResourceId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

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
            Level = level,
            ApprovalStatus = SkillApprovalStatus.Pending
        };
    }

    internal void SetLevel(SkillLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new DomainException($"Invalid skill level '{level}'.");
        Level = level;
    }

    internal void Approve(Guid reviewerResourceId, DateTime reviewedAt)
    {
        if (reviewerResourceId == Guid.Empty)
            throw new DomainException("Reviewer must be a valid resource.");
        if (ApprovalStatus != SkillApprovalStatus.Pending)
            throw new DomainException(
                $"Skill cannot be approved from state '{ApprovalStatus}'. Return it to Pending first.");

        ApprovalStatus = SkillApprovalStatus.Approved;
        ReviewedByResourceId = reviewerResourceId;
        ReviewedAt = reviewedAt;
    }

    internal void Reject(Guid reviewerResourceId, DateTime reviewedAt)
    {
        if (reviewerResourceId == Guid.Empty)
            throw new DomainException("Reviewer must be a valid resource.");
        if (ApprovalStatus != SkillApprovalStatus.Pending)
            throw new DomainException(
                $"Skill cannot be rejected from state '{ApprovalStatus}'. Return it to Pending first.");

        ApprovalStatus = SkillApprovalStatus.Rejected;
        ReviewedByResourceId = reviewerResourceId;
        ReviewedAt = reviewedAt;
    }

    internal void ReturnToPending()
    {
        if (ApprovalStatus == SkillApprovalStatus.Pending)
            throw new DomainException("Skill is already Pending.");

        ApprovalStatus = SkillApprovalStatus.Pending;
        ReviewedByResourceId = null;
        ReviewedAt = null;
    }
}
