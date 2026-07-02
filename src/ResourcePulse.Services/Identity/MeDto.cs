namespace ResourcePulse.Services.Identity;

// Identity of the calling user (gap #8 / ADR-0024). Lets the frontend implement
// "my projects" / "my open roles" filters without inventing identity from claims.
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

    // Whether the user may act as a staffing manager. Derived server-side from the
    // principal's role claim(s) so the client shares one rule (see MeService).
    public bool IsStaffingManager { get; init; }
}
