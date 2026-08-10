namespace ResourcePulse.Domain.Access;

/// <summary>
/// What a member of a tenant may do. Three roles, deliberately (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// The values are <b>ordered</b>, and the order is the authorization rule:
/// <c>Owner &gt; Planner &gt; Viewer</c>. A required role is satisfied by any
/// role at or above it, which is why the system needs three policies and no
/// permission matrix. Do not reorder or renumber — the comparison is the
/// hierarchy, not a lookup table that can drift away from it.
/// </para>
/// <para>
/// Roles are <b>ours</b>: they never come from the identity provider, which is
/// authoritative on identity and organization membership only (ADR-0029 §6).
/// </para>
/// </remarks>
public enum AppRole
{
    /// <summary>Reads everything in the tenant, writes nothing.</summary>
    Viewer = 0,

    /// <summary>Plans: projects, demands, coverage, and the operational registry.</summary>
    Planner = 1,

    /// <summary>Planner, plus the tenant's own settings and its members.</summary>
    Owner = 2
}
