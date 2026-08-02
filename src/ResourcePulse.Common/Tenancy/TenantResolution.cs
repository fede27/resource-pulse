namespace ResourcePulse.Common.Tenancy;

/// <summary>
/// Why a tenant resolution succeeded or failed. Every non-<see cref="Resolved"/>
/// outcome is a fail-close: the request is rejected, never downgraded to a
/// default or a "global" tenant (ADR-0029).
/// </summary>
public enum TenantResolutionOutcome
{
    /// <summary>The organization maps to an active tenant.</summary>
    Resolved,

    /// <summary>The validated token carried no organization claim.</summary>
    MissingOrganizationClaim,

    /// <summary>The organization is unknown to the control-plane registry.</summary>
    OrganizationNotMapped,

    /// <summary>The organization maps to a tenant that is not active.</summary>
    TenantNotActive
}

/// <summary>
/// Outcome of mapping an identity-provider organization to one of our tenants.
/// <see cref="TenantId"/> is meaningful only when <see cref="IsResolved"/>.
/// </summary>
public sealed record TenantResolution(
    TenantResolutionOutcome Outcome,
    Guid TenantId,
    string? OrganizationId)
{
    public bool IsResolved => Outcome == TenantResolutionOutcome.Resolved;

    public static TenantResolution Resolved(Guid tenantId, string organizationId) =>
        new(TenantResolutionOutcome.Resolved, tenantId, organizationId);

    public static TenantResolution MissingOrganizationClaim() =>
        new(TenantResolutionOutcome.MissingOrganizationClaim, Guid.Empty, null);

    public static TenantResolution OrganizationNotMapped(string organizationId) =>
        new(TenantResolutionOutcome.OrganizationNotMapped, Guid.Empty, organizationId);

    public static TenantResolution TenantNotActive(string organizationId) =>
        new(TenantResolutionOutcome.TenantNotActive, Guid.Empty, organizationId);
}
