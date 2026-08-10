namespace ResourcePulse.Http.Auth;

/// <summary>
/// The three authorization policies (ADR-0030). Named here because this is the
/// layer that annotates controllers; the requirement and its handler are wired in
/// <c>ResourcePulse.Hosting</c>.
/// </summary>
/// <remarks>
/// <para>
/// They are <b>hierarchical</b>, mirroring <c>AppRole</c>: <see cref="Owner"/>
/// implies <see cref="Planner"/> implies <see cref="Viewer"/>. Three roles and
/// three policies is the whole model — resist growing a per-endpoint permission
/// vocabulary on top, which is how a system stops being explainable.
/// </para>
/// <para>
/// These are <b>not</b> <c>[Authorize(Roles = ...)]</c>. No role is ever read from
/// a token: the handler reads our membership store, so there is no claim an
/// identity provider could mint to grant itself anything.
/// </para>
/// </remarks>
public static class AccessPolicies
{
    /// <summary>Any member of the tenant. Reads everything, writes nothing.</summary>
    public const string Viewer = "access:viewer";

    /// <summary>Plans work: projects, demands, coverage, operational registry.</summary>
    public const string Planner = "access:planner";

    /// <summary>Administers the tenant: its settings and its members.</summary>
    public const string Owner = "access:owner";
}
