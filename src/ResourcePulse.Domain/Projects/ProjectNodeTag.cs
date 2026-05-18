using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Projects;

// Owned by ProjectNode (any node type). Composite key (ProjectNodeId, TagId).
public sealed class ProjectNodeTag
{
    public Guid ProjectNodeId { get; private set; }
    public Guid TagId { get; private set; }

    private ProjectNodeTag() { }

    internal static ProjectNodeTag Create(Guid projectNodeId, Guid tagId)
    {
        if (tagId == Guid.Empty)
            throw new DomainException("ProjectNodeTag must reference a tag.");

        return new ProjectNodeTag
        {
            ProjectNodeId = projectNodeId,
            TagId = tagId
        };
    }
}
