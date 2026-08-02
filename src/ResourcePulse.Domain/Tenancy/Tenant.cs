using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Tenancy;

public enum TenantStatus
{
    Active,
    Suspended,
    Archived
}

/// <summary>
/// A customer organization, as WE know it. This is the control-plane registry
/// (ADR-0029): the identity provider is authoritative on identity and
/// organization membership only, never on our tenant identity. The IdP's
/// organization id is a *lookup key* into this table, not the tenant id itself,
/// so an organization can be re-pointed or a tenant re-federated without
/// rewriting every row of planning data.
/// </summary>
/// <remarks>
/// Deliberately NOT <see cref="IAuditable"/>: tenants are provisioned by machine
/// (the dev bootstrap, later a provisioning pipeline) outside any authenticated
/// user context, and <c>AuditInterceptor</c> hard-fails on an empty subject.
/// Timestamps are kept by the aggregate itself.
/// </remarks>
public sealed class Tenant : Entity<Guid>
{
    /// <summary>
    /// The identity provider's organization identifier (Zitadel `urn:zitadel:iam:org:id`).
    /// Unique across tenants: one organization maps to at most one tenant.
    /// </summary>
    public string IdentityOrganizationId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Create(string identityOrganizationId, string name)
    {
        var org = (identityOrganizationId ?? string.Empty).Trim();
        if (org.Length == 0)
            throw new DomainException("Tenant identity organization id must not be empty.");

        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            throw new DomainException("Tenant name must not be empty.");

        return new Tenant
        {
            Id = Guid.NewGuid(),
            IdentityOrganizationId = org,
            Name = trimmedName,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Tenant name must not be empty.");
        Name = trimmed;
        Touch();
    }

    /// <summary>
    /// Re-points the tenant at a different identity-provider organization. The
    /// tenant id — and therefore every planning row — is unaffected.
    /// </summary>
    public void RelinkIdentityOrganization(string identityOrganizationId)
    {
        var org = (identityOrganizationId ?? string.Empty).Trim();
        if (org.Length == 0)
            throw new DomainException("Tenant identity organization id must not be empty.");
        IdentityOrganizationId = org;
        Touch();
    }

    public void Activate() { Status = TenantStatus.Active; Touch(); }
    public void Suspend() { Status = TenantStatus.Suspended; Touch(); }
    public void Archive() { Status = TenantStatus.Archived; Touch(); }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
