using ResourcePulse.Domain.Access;

namespace ResourcePulse.Services.Access;

public sealed class MembershipReadDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public AppRole Role { get; init; }

    /// <summary>True while nobody has signed in against this grant yet.</summary>
    public bool IsPending { get; init; }

    /// <summary>
    /// The <c>Resource</c> this member is linked to (<c>Resource.UserSub</c>), if
    /// any. Membership and Resource are different things — most planned people
    /// never sign in — so this is frequently null and that is not a defect.
    /// </summary>
    public Guid? ResourceId { get; init; }

    /// <summary>True when this row is the caller's own membership.</summary>
    public bool IsSelf { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class InviteMembershipDto
{
    public string Email { get; init; } = string.Empty;
    public AppRole Role { get; init; } = AppRole.Viewer;
}

public sealed class UpdateMembershipRoleDto
{
    public AppRole Role { get; init; }
}
