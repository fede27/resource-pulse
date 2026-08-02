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
    /// Zitadel projects the organization id under this claim. Kept alongside the
    /// generic <c>org_id</c> so a different provider (or a test principal) can be
    /// slotted in without touching the resolution rule itself.
    /// </summary>
    public const string ZitadelOrgIdClaim = "urn:zitadel:iam:org:id";
    public const string GenericOrgIdClaim = "org_id";

    private static readonly string[] OrganizationClaimTypes = [ZitadelOrgIdClaim, GenericOrgIdClaim];

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
