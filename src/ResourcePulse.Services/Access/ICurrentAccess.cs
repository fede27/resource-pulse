using ResourcePulse.Domain.Access;

namespace ResourcePulse.Services.Access;

/// <summary>
/// What the caller may do in the current tenant (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// Populated after the tenant is resolved, by reading OUR membership store — never
/// from a token claim. That is what keeps "application roles do not come from the
/// identity provider" (ADR-0029 §6) a structural property rather than a rule
/// somebody has to remember: there is no claim to spoof, because no claim is read.
/// </para>
/// <para>
/// <see cref="Role"/> is null when the caller is anonymous, has no resolved
/// tenant, or is authenticated but holds no membership. All three mean "may do
/// nothing", and the authorization policies treat them alike.
/// </para>
/// </remarks>
public interface ICurrentAccess
{
    AppRole? Role { get; }

    bool IsMember { get; }

    /// <summary>
    /// Whether the caller holds <paramref name="required"/> or better. The role
    /// order <i>is</i> the hierarchy — see <see cref="AppRole"/>.
    /// </summary>
    bool Has(AppRole required);
}
