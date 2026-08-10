using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Services.Identity;

// Resolves "who am I" for the frontend (gap #8 / ADR-0024): auth subject + the
// linked Resource (Resource.UserSub == Sub) + job role + the application role
// this account holds in the tenant (ADR-0030). Read-only; no storage.
public sealed class MeService(
    ICurrentUserAccessor currentUser,
    ICurrentAccess currentAccess,
    ResourcePulseDbContext db) : IMeService
{
    public async Task<ServiceResult<MeDto>> GetAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
            return ServiceResult<MeDto>.Success(new MeDto { IsAuthenticated = false });

        var user = currentUser.User;

        // A non-member reaches this method by design: the endpoint is exempt from
        // the membership requirement precisely so the client can render "you have
        // no access here" instead of a blank screen (ADR-0030).
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
            IsMember = currentAccess.IsMember,
            AccessRole = currentAccess.Role
        });
    }
}
