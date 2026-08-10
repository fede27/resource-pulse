using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Access;

/// <summary>
/// Turns "who is calling" into "what they may do", by reading the membership
/// store of the current tenant (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// Two lookups, in order: by identity subject, then — only for a subject that
/// matches nothing — by the principal's email address, which claims a pending
/// invite. The second exists because a grant is necessarily written before its
/// holder has ever signed in, when their subject identifier does not exist yet.
/// </para>
/// <para>
/// <b>Ordering matters</b>: this reads a tenant-scoped, RLS-protected table, so it
/// must run after the tenant is in the Postgres session. Resolving access before
/// the tenant makes every query return nothing and turns everybody into a
/// non-member — a fail-close that reads like a configuration bug.
/// </para>
/// </remarks>
public sealed class AccessResolver(
    ICurrentUserAccessor currentUser,
    ITenantContext tenantContext,
    ResourcePulseDbContext db) : IAccessResolver
{
    public async Task<AccessResolution> ResolveAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !tenantContext.IsResolved)
            return AccessResolution.NotAuthenticated;

        var user = currentUser.User;
        if (string.IsNullOrWhiteSpace(user.Sub))
            return AccessResolution.NotAuthenticated;

        var bySub = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserSub == user.Sub)
            .Select(m => new { m.Id, m.Role })
            .FirstOrDefaultAsync(ct);

        if (bySub is not null)
            return AccessResolution.Member(bySub.Role, bySub.Id);

        return await ClaimPendingInviteAsync(user, ct);
    }

    /// <summary>
    /// Binds a pending invite to the subject that just signed in. Tracked (not
    /// <c>AsNoTracking</c>) because this path writes.
    /// </summary>
    private async Task<AccessResolution> ClaimPendingInviteAsync(CurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return AccessResolution.NotAMember;

        var pending = await db.Memberships
            .FirstOrDefaultAsync(m => m.Email == user.Email && m.UserSub == null, ct);

        if (pending is null)
            return AccessResolution.NotAMember;

        pending.ClaimBy(user.Sub, user.Name);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two sign-ins racing on the same invite: the loser violates the
            // per-tenant unique index on user_sub. The grant is the same either
            // way, so honour it rather than failing a login on a write we only
            // performed as a side effect of reading.
            db.ChangeTracker.Clear();

            var claimed = await db.Memberships
                .AsNoTracking()
                .Where(m => m.UserSub == user.Sub)
                .Select(m => new { m.Id, m.Role })
                .FirstOrDefaultAsync(ct);

            return claimed is null
                ? AccessResolution.NotAMember
                : AccessResolution.Member(claimed.Role, claimed.Id);
        }

        return AccessResolution.Member(pending.Role, pending.Id);
    }
}
