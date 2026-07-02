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
    // Role-claim values that grant staffing-manager capability. Both the short
    // "role" claim type (FakeAuth / many OIDC providers) and ClaimTypes.Role are
    // inspected. Centralised here so the client never re-derives it.
    private static readonly HashSet<string> StaffingManagerRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "StaffingManager" };

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

    private static bool IsStaffingManager(CurrentUser user)
    {
        foreach (var key in new[] { "role", ClaimTypes.Role })
            if (user.Claims.TryGetValue(key, out var value) && StaffingManagerRoles.Contains(value))
                return true;
        return false;
    }
}
