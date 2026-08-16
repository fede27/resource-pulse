using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Http.Access;

/// <summary>
/// Who may act in this tenant (ADR-0030).
/// </summary>
/// <remarks>
/// Owner-only, from the moment it exists. The rest of the endpoint surface is
/// mapped to policies in a later step, but this one cannot wait for it: an
/// unguarded write endpoint over the authorization store is a
/// privilege-escalation path — any authenticated caller could grant themselves
/// Owner — so it ships guarded or not at all.
/// </remarks>
[Route("api/memberships")]
[ApiController]
[RequireOwner]
public sealed class MembershipsController(IMembershipService service) : ControllerFoundation
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MembershipReadDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct) =>
        FromResult(await service.GetAllAsync(ct));

    [HttpPost]
    [ProducesResponseType<MembershipReadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteAsync([FromBody] InviteMembershipDto dto, CancellationToken ct) =>
        FromCreateResult(await service.InviteAsync(dto, ct), m => m.Id);

    [HttpPut("{id}/role")]
    [ProducesResponseType<MembershipReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRoleAsync(
        Guid id,
        [FromBody] UpdateMembershipRoleDto dto,
        CancellationToken ct) =>
        FromResult(await service.ChangeRoleAsync(id, dto, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeAsync(Guid id, CancellationToken ct) =>
        FromResult(await service.RevokeAsync(id, ct));
}
