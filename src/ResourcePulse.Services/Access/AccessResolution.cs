using ResourcePulse.Domain.Access;

namespace ResourcePulse.Services.Access;

public enum AccessOutcome
{
    /// <summary>The caller holds a membership in the current tenant.</summary>
    Member,

    /// <summary>Nothing to resolve against — no authenticated principal.</summary>
    NotAuthenticated,

    /// <summary>
    /// Authenticated, tenant resolved, but no membership matches. Fail-close: an
    /// organization mapping proves which tenant someone belongs to, never that
    /// anyone granted them access to it.
    /// </summary>
    NotAMember
}

public sealed record AccessResolution(AccessOutcome Outcome, AppRole? Role, Guid? MembershipId)
{
    public bool IsMember => Outcome == AccessOutcome.Member;

    public static AccessResolution Member(AppRole role, Guid membershipId) =>
        new(AccessOutcome.Member, role, membershipId);

    public static readonly AccessResolution NotAuthenticated =
        new(AccessOutcome.NotAuthenticated, null, null);

    public static readonly AccessResolution NotAMember =
        new(AccessOutcome.NotAMember, null, null);
}
