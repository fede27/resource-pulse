using ResourcePulse.Domain.Access;

namespace ResourcePulse.Services.Identity;

// Identity of the calling user (gap #8 / ADR-0024), plus what they may do
// (ADR-0030). Lets the frontend implement "my projects" / "my open roles" filters
// and gate write gestures without inventing either from claims.
public sealed class MeDto
{
    public bool IsAuthenticated { get; init; }
    public string Sub { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    // Display name: the linked Resource's name when the user is linked, otherwise
    // the auth "name" claim.
    public string Name { get; init; } = string.Empty;

    // The Resource this user is linked to (Resource.UserSub == Sub), if any. Null
    // when the authenticated principal has no matching resource row.
    public Guid? ResourceId { get; init; }
    public Guid? RoleId { get; init; }
    public string? RoleName { get; init; }

    /// <summary>
    /// Whether a membership grants this user access to the current tenant. False
    /// is a legitimate, fully-rendered state — the client shows "no access" rather
    /// than an empty application (ADR-0030).
    /// </summary>
    public bool IsMember { get; init; }

    /// <summary>
    /// The user's application role in this tenant, or null when not a member.
    /// Read from OUR membership store; never from a token claim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named <c>AccessRole</c>, not <c>Role</c>, because <c>RoleId</c>/
    /// <c>RoleName</c> above are the person's <i>job</i> role from the domain
    /// catalogue (Developer, PM). The two are unrelated and collapsing the word
    /// would fuse "what this person does" with "what this account may do".
    /// </para>
    /// <para>
    /// This replaces <c>IsStaffingManager</c>, which read a role claim and was
    /// carried as declared debt in ADR-0029 §6. The capability it stood for is
    /// now "<c>Planner</c> or better".
    /// </para>
    /// </remarks>
    public AppRole? AccessRole { get; init; }
}
