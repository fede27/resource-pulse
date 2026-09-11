using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ResourcePulse.Http;
using ResourcePulse.Http.Auth;

namespace ResourcePulse.Application.Tests;

/// <summary>
/// Audits the whole controller surface against the role mapping (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>static</b> audit by reflection rather than a set of HTTP round
/// trips, and that is the point: the failure this guards against is not "the
/// policy evaluates wrongly" — one handler decides that for everyone, and its
/// hierarchy is tested elsewhere — but "somebody added a write endpoint and
/// forgot to annotate it". A per-endpoint integration test would need a database
/// and would still only cover the endpoints somebody remembered to list.
/// </para>
/// <para>
/// The expected mapping below is an explicit inventory, in the same spirit as the
/// RLS table list in the tenancy migration: a new controller has to be added here
/// deliberately, and until it is, <see cref="EveryControllerIsMapped"/> fails.
/// </para>
/// </remarks>
public class EndpointAuthorizationTests
{
    private static readonly string[] WriteVerbs = ["POST", "PUT", "DELETE", "PATCH"];

    /// <summary>Controller name → the policy its WRITE actions must require.</summary>
    private static readonly Dictionary<string, string> WritePolicyByController = new()
    {
        // Planning
        ["PlanCommandsController"] = AccessPolicies.Planner,
        ["ProjectsController"] = AccessPolicies.Planner,
        ["ProjectNodesController"] = AccessPolicies.Planner,
        // Imposed dates on a project (ADR-0034 §4): a project artefact, planned by
        // the same hands that plan the project.
        ["ExternalConstraintsController"] = AccessPolicies.Planner,
        // Operational registry — a planner who cannot add the person they are
        // about to staff is crippled.
        ["ResourcesController"] = AccessPolicies.Planner,
        ["TeamsController"] = AccessPolicies.Planner,
        ["RolesController"] = AccessPolicies.Planner,
        ["SkillsController"] = AccessPolicies.Planner,
        ["TagsController"] = AccessPolicies.Planner,
        // Tenant configuration — a calendar changes everyone's capacity.
        ["BusinessCalendarsController"] = AccessPolicies.Owner,
        ["CompanyClosuresController"] = AccessPolicies.Owner,
        ["LoadBandsController"] = AccessPolicies.Owner,
        ["TimeFenceController"] = AccessPolicies.Owner,
        ["BucketingController"] = AccessPolicies.Owner,
        ["CommitmentPolicyController"] = AccessPolicies.Owner,
        ["SignalPolicyController"] = AccessPolicies.Owner,
        // The authorization store itself.
        ["MembershipsController"] = AccessPolicies.Owner,
        // Triage (ADR-0032): accepting or reopening a risk is a planning decision.
        // One of its writes is deliberately looser — see WritePolicyByAction.
        ["SignalsController"] = AccessPolicies.Planner,
        // Development-only role switch: deliberately Viewer, so the way back stays
        // open after self-demotion (see DevAccessService).
        ["DevAccessController"] = AccessPolicies.Viewer,
    };

    /// <summary>
    /// The rare write that must be looser than its controller, keyed
    /// <c>Controller.Action</c>. Checked before
    /// <see cref="WritePolicyByController"/>.
    /// </summary>
    /// <remarks>
    /// Per-action rather than per-controller because the alternative — splitting a
    /// controller so the mapping stays one-policy-wide — would let the shape of an
    /// audit dictate the shape of the API. Each entry has to justify itself here,
    /// which is the same bar the controller-level mapping sets.
    /// </remarks>
    private static readonly Dictionary<string, string> WritePolicyByAction = new()
    {
        // A personal bookmark, not plan data (ADR-0032 §5). Guarded Viewer for the
        // same reason the development role switch is: refuse it and the "unseen"
        // dot never works for the very people it exists for.
        ["SignalsController.RecordVisit"] = AccessPolicies.Viewer,
    };

    /// <summary>
    /// Controllers with no write action at all. Listed so that adding a write to
    /// one of them trips <see cref="EveryControllerIsMapped"/> instead of shipping
    /// unguarded.
    /// </summary>
    private static readonly string[] ReadOnlyControllers =
        ["AllocationsController", "DemandsController", "LoadController", "MeController"];

    /// <summary>
    /// Controllers reachable with no principal at all (ADR-0031).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A third category, declared for the same reason as the other two: an
    /// anonymous write endpoint is the one thing this audit exists to catch, so it
    /// must be impossible to add one <i>quietly</i>. Adding a controller here is a
    /// deliberate act, and <see cref="AnonymousControllersReallyAreAnonymous"/>
    /// plus <see cref="OnlyDeclaredControllersAreAnonymous"/> close both directions:
    /// nothing listed here is secretly guarded, and nothing guarded-looking is
    /// secretly anonymous.
    /// </para>
    /// <para>
    /// <b>LoginController</b> earns it structurally: it serves the sign-in page,
    /// whose caller has no token, therefore no organization, therefore no tenant
    /// and no membership. It grants nothing — it only relays a password check to
    /// the identity provider. Authorization still happens where it always did,
    /// against our membership store, once the token comes back.
    /// </para>
    /// </remarks>
    private static readonly string[] AnonymousControllers = ["LoginController"];

    private static bool IsAnonymous(Type controller) =>
        controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    private static IEnumerable<Type> Controllers() =>
        typeof(ControllerFoundation).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t));

    private static IEnumerable<(Type Controller, MethodInfo Action, string[] Verbs)> Actions() =>
        from controller in Controllers()
        from method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        let verbs = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .ToArray()
        where verbs.Length > 0
        select (controller, method, verbs);

    private static bool IsWrite(string[] verbs) => verbs.Any(v => WriteVerbs.Contains(v));

    /// <summary>The policy in force for an action: its own attribute, else its controller's.</summary>
    private static string? EffectivePolicy(Type controller, MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Policy).FirstOrDefault()
        ?? controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Policy).FirstOrDefault();

    // The regression that started this: every write endpoint fell through to a
    // fallback that only required authentication, so a Viewer — and in fact any
    // authenticated non-member — could create a project.
    [Fact]
    public void EveryWriteEndpointRequiresItsMappedRole()
    {
        var writes = Actions().Where(a => IsWrite(a.Verbs)).ToList();

        // Non-vacuity guard: if the reflection ever stops finding actions — a
        // renamed base class, a changed binding flag — every assertion below would
        // pass over an empty list and this audit would silently stop auditing.
        writes.Should().HaveCountGreaterThan(60,
            "the write surface was ~72 endpoints when this audit was written");

        var offenders = new List<string>();

        foreach (var (controller, action, verbs) in writes)
        {
            // Declared anonymous surface (ADR-0031). Skipped here, but not
            // unchecked: the two tests below verify the declaration matches reality
            // in both directions.
            if (AnonymousControllers.Contains(controller.Name)) continue;

            if (!WritePolicyByAction.TryGetValue($"{controller.Name}.{action.Name}", out var expected) &&
                !WritePolicyByController.TryGetValue(controller.Name, out expected))
            {
                offenders.Add($"{controller.Name}.{action.Name} [{string.Join('/', verbs)}] — controller not mapped");
                continue;
            }

            var actual = EffectivePolicy(controller, action);
            if (actual != expected)
                offenders.Add($"{controller.Name}.{action.Name} [{string.Join('/', verbs)}] — expected {expected}, found {actual ?? "<none>"}");
        }

        offenders.Should().BeEmpty();
    }

    // Reads carry no attribute on purpose — the fallback policy is Viewer, so an
    // un-annotated endpoint already demands membership. What must never happen is
    // a read demanding MORE than the mapping says, which would silently hide data
    // from people entitled to it.
    [Fact]
    public void ReadEndpointsDoNotDemandMoreThanTheirControllersWrites()
    {
        var offenders = new List<string>();

        foreach (var (controller, action, verbs) in Actions().Where(a => !IsWrite(a.Verbs)))
        {
            var actual = EffectivePolicy(controller, action);
            if (actual is null or AccessPolicies.Viewer) continue;

            // A read may inherit a stricter controller-level policy, but only where
            // the whole controller is that strict by design (memberships).
            if (WritePolicyByController.TryGetValue(controller.Name, out var expected) && actual == expected) continue;

            offenders.Add($"{controller.Name}.{action.Name} [{string.Join('/', verbs)}] — read requires {actual}");
        }

        offenders.Should().BeEmpty();
    }

    // GET /api/me is THE exemption: it must stay reachable by a non-member, or the
    // client cannot render "you have no access" and shows a blank screen instead.
    [Fact]
    public void OnlyMeIsExemptFromTheMembershipRequirement()
    {
        var exempt = Controllers()
            .Where(c => c.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(a => a.Policy is null))
            .Select(c => c.Name)
            .ToArray();

        exempt.Should().BeEquivalentTo(["MeController"]);
    }

    // Application roles are ours: a role claim is something the identity provider
    // controls, so [Authorize(Roles = ...)] must never appear (ADR-0029 §6).
    [Fact]
    public void NoEndpointAuthorizesOnARoleClaim()
    {
        var offenders =
            from controller in Controllers()
            from attribute in controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)))
            where !string.IsNullOrEmpty(attribute.Roles)
            select controller.Name;

        offenders.Should().BeEmpty();
    }

    // The declaration must not become a way to exempt a controller on paper while
    // it is in fact guarded — or, far worse, the reverse.
    [Fact]
    public void AnonymousControllersReallyAreAnonymous()
    {
        var offenders = AnonymousControllers
            .Select(name => Controllers().SingleOrDefault(c => c.Name == name) is { } c && IsAnonymous(c)
                ? null
                : $"{name} is declared anonymous but does not carry [AllowAnonymous]")
            .Where(x => x is not null);

        offenders.Should().BeEmpty();
    }

    // The direction that actually protects data: an [AllowAnonymous] added to any
    // other controller drops it out of the Viewer fallback entirely, and this is
    // the only test that would notice.
    [Fact]
    public void OnlyDeclaredControllersAreAnonymous()
    {
        Controllers()
            .Where(IsAnonymous)
            .Select(c => c.Name)
            .Should().BeEquivalentTo(AnonymousControllers);
    }

    // A per-action override that no longer overrides anything is worse than no
    // override: it reads as a documented exception while the endpoint has quietly
    // moved back under its controller's policy — or vanished.
    [Fact]
    public void EveryPerActionOverrideStillPointsAtARealAction()
    {
        var actual = Actions()
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToHashSet();

        WritePolicyByAction.Keys.Except(actual).Should().BeEmpty();
    }

    [Fact]
    public void EveryControllerIsMapped()
    {
        var known = WritePolicyByController.Keys
            .Concat(ReadOnlyControllers)
            .Concat(AnonymousControllers)
            .ToHashSet();
        var actual = Controllers().Select(c => c.Name).ToHashSet();

        actual.Except(known).Should().BeEmpty("a new controller must be mapped to a role deliberately");
        known.Except(actual).Should().BeEmpty("the mapping lists a controller that no longer exists");
    }

    [Fact]
    public void ReadOnlyControllersReallyHaveNoWrites()
    {
        var withWrites = Actions()
            .Where(a => IsWrite(a.Verbs) && ReadOnlyControllers.Contains(a.Controller.Name))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}");

        withWrites.Should().BeEmpty();
    }
}
