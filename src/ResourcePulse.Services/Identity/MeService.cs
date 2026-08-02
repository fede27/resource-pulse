using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Identity;

// Resolves "who am I" for the frontend (gap #8 / ADR-0024): auth subject +
// the linked Resource (Resource.UserSub == Sub) + role + a derived
// staffing-manager flag. Read-only; no storage.
public sealed class MeService(
    ICurrentUserAccessor currentUser,
    ResourcePulseDbContext db) : IMeService
{
    // Role-claim values that grant staffing-manager capability.
    //
    // KNOWN DEBT (ADR-0029). Application roles are OUR domain: the identity
    // provider is authoritative on identity and organization membership only, and
    // must never be the source of an authorization decision. This claim-reading
    // path therefore applies to the FakeAuth development scheme ONLY — it is what
    // keeps the local dev loop usable until the capability moves into an
    // authorization store of ours. Under any real IdP scheme the flag is false;
    // see IsStaffingManager below.
    private static readonly HashSet<string> StaffingManagerRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "StaffingManager" };

    /// <summary>
    /// The only authentication scheme whose role claims are trusted, and only
    /// because it is a local development stub with no external issuer.
    /// </summary>
    private const string DevelopmentFakeScheme = "FakeAuth";

    public async Task<ServiceResult<MeDto>> GetAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
            return ServiceResult<MeDto>.Success(new MeDto { IsAuthenticated = false });

        var user = currentUser.User;

        var resource = await db.Resources.AsNoTracking()
            .Where(r => r.UserSub == user.Sub)
            .Select(r => new { r.Id, r.RoleId, r.Name })
            .FirstOrDefaultAsync(ct);

        string? roleName = null;
        if (resource?.RoleId is { } roleId)
            roleName = await db.Roles.AsNoTracking()
                .Where(r => r.Id == roleId)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(ct);

        return ServiceResult<MeDto>.Success(new MeDto
        {
            IsAuthenticated = true,
            Sub = user.Sub,
            Email = user.Email,
            Name = resource?.Name ?? user.Name,
            ResourceId = resource?.Id,
            RoleId = resource?.RoleId,
            RoleName = roleName,
            IsStaffingManager = IsStaffingManager(user)
        });
    }

    private bool IsStaffingManager(CurrentUser user)
    {
        // Fail closed for every real identity provider: a role claim minted
        // outside our system grants nothing. Only the local FakeAuth stub is
        // honoured, and only in development.
        if (!string.Equals(currentUser.AuthenticationScheme, DevelopmentFakeScheme, StringComparison.Ordinal))
            return false;

        foreach (var key in new[] { "role", ClaimTypes.Role })
            if (user.Claims.TryGetValue(key, out var value) && StaffingManagerRoles.Contains(value))
                return true;

        return false;
    }
}
