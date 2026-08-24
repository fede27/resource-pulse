using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Tenancy;
using ResourcePulse.Persistence.ControlPlane;

namespace ResourcePulse.Services.Tenancy;

/// <summary>
/// Resolves org -> TenantId against the control-plane registry (ADR-0029).
/// <para>
/// Every failure mode is a <b>fail-close</b>: an absent claim, an unknown
/// organization or a non-active tenant all yield a non-resolved outcome, which
/// the middleware turns into 403. There is deliberately no fallback to a default
/// tenant, no "first tenant wins", and no auto-provisioning — an unmapped
/// organization is an operator decision, not something the request path invents.
/// </para>
/// </summary>
public sealed class TenantResolver(ControlPlaneDbContext controlPlane) : ITenantResolver
{
    /// <summary>
    /// The claim Zitadel actually emits for <i>the organization this user belongs
    /// to</i>, and therefore the one that resolves a real token.
    /// </summary>
    /// <remarks>
    /// It appears only when the client requests the reserved scope
    /// <c>urn:zitadel:iam:user:resourceowner</c> — see the SPA's
    /// <c>auth/config.ts</c>. Without that scope Zitadel emits no organization
    /// claim at all, resolution fails, and <b>every</b> authenticated request is
    /// rejected with 403, <c>GET /api/me</c> included: that endpoint is exempt
    /// from the membership requirement (ADR-0030), never from tenant resolution,
    /// which runs before it and rejects unconditionally.
    /// </remarks>
    public const string ZitadelResourceOwnerIdClaim = "urn:zitadel:iam:user:resourceowner:id";

    /// <summary>
    /// Zitadel's <i>enforced-organization</i> claim, echoed when a client pins a
    /// specific org with <c>urn:zitadel:iam:org:id:{id}</c>. We do not pin one, so
    /// a real token never carries it — but FakeAuth issues it, and it stays the
    /// canonical name a hand-built principal uses.
    /// </summary>
    public const string ZitadelOrgIdClaim = "urn:zitadel:iam:org:id";
    public const string GenericOrgIdClaim = "org_id";

    /// <summary>
    /// Tried in order. The rule itself is unchanged — an organization claim is
    /// looked up in the registry, and anything missing or unmapped fails close;
    /// this list only says which claim names carry that organization.
    /// </summary>
    private static readonly string[] OrganizationClaimTypes =
        [ZitadelResourceOwnerIdClaim, ZitadelOrgIdClaim, GenericOrgIdClaim];

    public Task<TenantResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return ResolveByOrganizationAsync(ReadOrganizationClaim(principal), ct);
    }

    public async Task<TenantResolution> ResolveByOrganizationAsync(
        string? organizationId,
        CancellationToken ct = default)
    {
        var org = organizationId?.Trim();
        if (string.IsNullOrEmpty(org))
            return TenantResolution.MissingOrganizationClaim();

        // The registry is intentionally NOT tenant-filtered: it is what
        // establishes the tenant in the first place.
        var match = await controlPlane.Tenants
            .AsNoTracking()
            .Where(t => t.IdentityOrganizationId == org)
            .Select(t => new { t.Id, t.Status })
            .FirstOrDefaultAsync(ct);

        if (match is null)
            return TenantResolution.OrganizationNotMapped(org);

        if (match.Status != TenantStatus.Active)
            return TenantResolution.TenantNotActive(org);

        return TenantResolution.Resolved(match.Id, org);
    }

    private static string? ReadOrganizationClaim(ClaimsPrincipal principal)
    {
        foreach (var claimType in OrganizationClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
