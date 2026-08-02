using System.Security.Claims;
using ResourcePulse.Common.Tenancy;

namespace ResourcePulse.Services.Tenancy;

/// <summary>
/// Maps a validated principal's identity-provider organization onto one of our
/// tenants, via the control-plane registry (ADR-0029).
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves the tenant for an already-validated principal. Never trusts
    /// anything but the token: no headers, no route values, no subdomains.
    /// </summary>
    Task<TenantResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>Resolves by organization id directly. Same fail-close semantics.</summary>
    Task<TenantResolution> ResolveByOrganizationAsync(string? organizationId, CancellationToken ct = default);
}
