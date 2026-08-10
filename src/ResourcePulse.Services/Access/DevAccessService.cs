using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Access;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Access;

/// <inheritdoc cref="IDevAccessService"/>
/// <remarks>
/// <para>
/// The one deliberate exception to <c>MembershipService</c>'s "you cannot change
/// your own role" guard — self-demotion is the entire point here, since the
/// developer is usually the tenant's only member.
/// </para>
/// <para>
/// Neither is the <b>last-owner</b> guard applied, and that is safe for a specific
/// reason rather than by oversight: this endpoint requires <i>membership</i>, not
/// Owner. Dropping yourself to Viewer therefore leaves the way back open — the
/// same call promotes you again. Requiring Owner here would be the change that
/// turns a reversible switch into a one-way lockout.
/// </para>
/// </remarks>
public sealed class DevAccessService(
    ResourcePulseDbContext db,
    ICurrentUserAccessor currentUser) : IDevAccessService
{
    public async Task<ServiceResult<AppRole>> ActAsAsync(AppRole role, CancellationToken ct = default)
    {
        var sub = currentUser.User.Sub;
        if (string.IsNullOrWhiteSpace(sub))
            return ServiceResult<AppRole>.Conflict("No authenticated subject to act as.");

        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.UserSub == sub, ct);
        if (membership is null)
            return ServiceResult<AppRole>.NotFound(
                "You hold no membership in this tenant, so there is no role to change.");

        membership.ChangeRole(role, sub);
        await db.SaveChangesAsync(ct);

        return ServiceResult<AppRole>.Success(role);
    }
}
