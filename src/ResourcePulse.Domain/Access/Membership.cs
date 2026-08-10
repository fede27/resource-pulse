using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Access;

/// <summary>
/// A person's right to act inside a tenant, and at what level (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// This is the authorization store, and it is <b>ours</b> — the identity provider
/// is never asked what someone may do (ADR-0029 §6). It is tenant data like any
/// other: tenant-scoped, row-level-security protected. It is deliberately not in
/// the control plane, which exists to <i>establish</i> the tenant and is read
/// before one is known.
/// </para>
/// <para>
/// <b>Membership ≠ Resource.</b> A <c>Resource</c> is someone whose time is
/// planned; a membership is someone who signs in. Most planned people never sign
/// in, and some members (an external sponsor, an admin) are never planned. The
/// two are joined, when they are, through <c>Resource.UserSub</c>.
/// </para>
/// <para>
/// <b>Email is the invite key, UserSub is the claim.</b> A membership is granted
/// before its holder has ever signed in, when their subject identifier does not
/// exist yet: an IdP mints it on first login and it cannot be predicted. So the
/// row is created against the email address and <see cref="ClaimBy"/> binds it to
/// a subject the first time that person actually arrives.
/// </para>
/// <para>
/// Deliberately NOT <see cref="IAuditable"/>, for the same reason as
/// <c>Tenant</c>: memberships are written by machine (dev seeding, provisioning)
/// outside any authenticated user context, and <c>AuditInterceptor</c> hard-fails
/// on an empty subject. Provenance is kept by the aggregate itself.
/// </para>
/// </remarks>
public sealed class Membership : Entity<Guid>
{
    /// <summary>
    /// The address the membership was granted to. Unique per tenant, and the key
    /// a not-yet-signed-in member is matched on. Case-insensitive at the database
    /// level (citext), like <c>Resource.Email</c>.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// The identity subject this membership is bound to, or null while the invite
    /// is unclaimed. Set exactly once, by <see cref="ClaimBy"/>.
    /// </summary>
    public string? UserSub { get; private set; }

    /// <summary>Display name captured at claim time; purely informational.</summary>
    public string? DisplayName { get; private set; }

    public AppRole Role { get; private set; } = AppRole.Viewer;

    /// <summary>The subject that granted or last changed this role, when known.</summary>
    public string? GrantedByUserSub { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>True while nobody has signed in against this grant yet.</summary>
    public bool IsPending => UserSub is null;

    private Membership() { }

    /// <summary>
    /// Grants a role to an email address. The holder need not exist yet anywhere —
    /// not in the identity provider, not as a <c>Resource</c>.
    /// </summary>
    public static Membership Invite(string email, AppRole role, string? grantedByUserSub = null)
    {
        var normalizedEmail = NormalizeEmail(email);

        return new Membership
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Role = role,
            GrantedByUserSub = Normalize(grantedByUserSub),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Binds an unclaimed membership to the subject that just signed in.
    /// </summary>
    /// <remarks>
    /// Idempotent for the same subject, so a concurrent double login is harmless.
    /// Re-claiming by a <i>different</i> subject throws: silently re-pointing a
    /// grant would hand one person's access to another, which is the one failure
    /// mode this aggregate exists to prevent.
    /// </remarks>
    public void ClaimBy(string userSub, string? displayName = null)
    {
        var subject = Normalize(userSub)
            ?? throw new DomainException("Membership user subject must not be empty.");

        if (UserSub is not null && !string.Equals(UserSub, subject, StringComparison.Ordinal))
            throw new DomainException(
                $"Membership for '{Email}' is already claimed by another identity subject.");

        if (UserSub == subject && Normalize(displayName) == DisplayName) return;

        UserSub = subject;
        DisplayName = Normalize(displayName) ?? DisplayName;
        Touch();
    }

    public void ChangeRole(AppRole role, string? grantedByUserSub = null)
    {
        if (Role == role) return;

        Role = role;
        GrantedByUserSub = Normalize(grantedByUserSub) ?? GrantedByUserSub;
        Touch();
    }

    /// <summary>
    /// Re-points the grant at a different address, for an invite sent to the wrong
    /// one. Refused once claimed: at that point the subject, not the address, is
    /// the identity, and editing the address would be a no-op dressed up as a fix.
    /// </summary>
    public void ChangeEmail(string email)
    {
        if (!IsPending)
            throw new DomainException(
                "A claimed membership's email is set by the identity provider and cannot be changed here.");

        var normalized = NormalizeEmail(email);
        if (normalized == Email) return;

        Email = normalized;
        Touch();
    }

    private static string NormalizeEmail(string email)
    {
        var trimmed = Normalize(email)
            ?? throw new DomainException("Membership email must not be empty.");

        // Deliberately shallow: the database column is citext and the identity
        // provider is the authority on what a deliverable address is. This guards
        // against the obvious paste error, not against RFC 5322.
        if (!trimmed.Contains('@', StringComparison.Ordinal))
            throw new DomainException($"'{trimmed}' is not a valid email address.");

        return trimmed;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
