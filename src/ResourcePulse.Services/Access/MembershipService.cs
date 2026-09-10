using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Access;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Access;

/// <summary>
/// Administers who may act in this tenant (ADR-0030).
/// </summary>
/// <remarks>
/// Two guards live here rather than on the aggregate, because both are statements
/// about the <i>set</i> of memberships or about the caller — neither is knowable
/// from a single <see cref="Membership"/>.
/// </remarks>
public sealed class MembershipService(
    ResourcePulseDbContext db,
    ICurrentUserAccessor currentUser) : IMembershipService
{
    public async Task<ServiceResult<IReadOnlyList<MembershipReadDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await db.Memberships
            .AsNoTracking()
            .OrderBy(m => m.Email)
            .Select(m => new
            {
                m.Id,
                m.Email,
                m.DisplayName,
                m.Role,
                m.UserSub,
                m.CreatedAt,
                m.UpdatedAt
            })
            .ToListAsync(ct);

        // Batch FK -> name resolution (pattern of ADR-0024): one query for the
        // whole page rather than one per row.
        var subs = rows.Where(r => r.UserSub is not null).Select(r => r.UserSub!).ToList();

        var resourceIdBySub = subs.Count == 0
            ? []
            : await db.Resources
                .AsNoTracking()
                .Where(r => r.UserSub != null && subs.Contains(r.UserSub))
                .ToDictionaryAsync(r => r.UserSub!, r => r.Id, ct);

        var callerSub = currentUser.User.Sub;

        var dtos = rows
            .Select(r => new MembershipReadDto
            {
                Id = r.Id,
                Email = r.Email,
                DisplayName = r.DisplayName,
                Role = r.Role,
                IsPending = r.UserSub is null,
                ResourceId = r.UserSub is not null && resourceIdBySub.TryGetValue(r.UserSub, out var resourceId)
                    ? resourceId
                    : null,
                IsSelf = r.UserSub is not null && r.UserSub == callerSub,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToList();

        return ServiceResult<IReadOnlyList<MembershipReadDto>>.Success(dtos);
    }

    public async Task<ServiceResult<MembershipReadDto>> InviteAsync(
        InviteMembershipDto dto,
        CancellationToken ct = default)
    {
        Membership membership;
        try
        {
            membership = Membership.Invite(dto.Email, dto.Role, currentUser.User.Sub);
        }
        catch (DomainException ex)
        {
            return ServiceResult<MembershipReadDto>.Conflict(ex.Message);
        }

        db.Memberships.Add(membership);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ServiceResult<MembershipReadDto>.Conflict(
                $"'{membership.Email}' already has a membership in this tenant.");
        }

        return ServiceResult<MembershipReadDto>.Success(ToDto(membership, currentUser.User.Sub));
    }

    public async Task<ServiceResult<MembershipReadDto>> ChangeRoleAsync(
        Guid id,
        UpdateMembershipRoleDto dto,
        CancellationToken ct = default)
    {
        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (membership is null)
            return ServiceResult<MembershipReadDto>.NotFound($"Membership {id} not found.");

        if (IsSelf(membership))
            return ServiceResult<MembershipReadDto>.Conflict(
                "You cannot change your own role. Ask another owner to do it.");

        if (await WouldRemoveLastOwnerAsync(membership, dto.Role, ct))
            return ServiceResult<MembershipReadDto>.Conflict(
                "This is the last owner of the tenant. Promote another member to owner first.");

        membership.ChangeRole(dto.Role, currentUser.User.Sub);
        await db.SaveChangesAsync(ct);

        return ServiceResult<MembershipReadDto>.Success(ToDto(membership, currentUser.User.Sub));
    }

    public async Task<ServiceResult<Unit>> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (membership is null) return ServiceResult.NotFound($"Membership {id} not found.");

        if (IsSelf(membership))
            return ServiceResult.Conflict(
                "You cannot revoke your own membership. Ask another owner to do it.");

        if (await WouldRemoveLastOwnerAsync(membership, newRole: null, ct))
            return ServiceResult.Conflict(
                "This is the last owner of the tenant. Promote another member to owner first.");

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(ct);

        return ServiceResult.Ok();
    }

    private bool IsSelf(Membership membership) =>
        membership.UserSub is not null &&
        membership.UserSub == currentUser.User.Sub;

    /// <summary>
    /// Whether demoting (<paramref name="newRole"/> set) or removing (null) this
    /// membership would leave the tenant with no owner — and therefore with nobody
    /// who can ever grant one again. Pending owner invites count: they are a grant
    /// somebody made, and re-inviting is always available if one goes unclaimed.
    /// </summary>
    private async Task<bool> WouldRemoveLastOwnerAsync(
        Membership membership,
        AppRole? newRole,
        CancellationToken ct)
    {
        if (membership.Role != AppRole.Owner) return false;
        if (newRole == AppRole.Owner) return false;

        var owners = await db.Memberships.CountAsync(m => m.Role == AppRole.Owner, ct);
        return owners <= 1;
    }

    private static MembershipReadDto ToDto(Membership m, string callerSub) => new()
    {
        Id = m.Id,
        Email = m.Email,
        DisplayName = m.DisplayName,
        Role = m.Role,
        IsPending = m.IsPending,
        IsSelf = m.UserSub is not null && m.UserSub == callerSub,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
