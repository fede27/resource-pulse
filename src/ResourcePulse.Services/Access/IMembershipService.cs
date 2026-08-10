using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Access;

public interface IMembershipService
{
    Task<ServiceResult<IReadOnlyList<MembershipReadDto>>> GetAllAsync(CancellationToken ct = default);

    Task<ServiceResult<MembershipReadDto>> InviteAsync(InviteMembershipDto dto, CancellationToken ct = default);

    Task<ServiceResult<MembershipReadDto>> ChangeRoleAsync(
        Guid id,
        UpdateMembershipRoleDto dto,
        CancellationToken ct = default);

    Task<ServiceResult<Unit>> RevokeAsync(Guid id, CancellationToken ct = default);
}
