using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Resources;

// Owned by Resource; composite key (ResourceId, TagId). Pure join — no payload.
public sealed class ResourceTag
{
    public Guid ResourceId { get; private set; }
    public Guid TagId { get; private set; }

    private ResourceTag() { }

    internal static ResourceTag Create(Guid resourceId, Guid tagId)
    {
        if (tagId == Guid.Empty)
            throw new DomainException("ResourceTag must reference a tag.");

        return new ResourceTag
        {
            ResourceId = resourceId,
            TagId = tagId
        };
    }
}
