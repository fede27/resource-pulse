using Microsoft.AspNetCore.Authorization;

namespace ResourcePulse.Http.Auth;

/// <summary>
/// The three role gates, as attributes (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// Sugar over <c>[Authorize(Policy = AccessPolicies.X)]</c>, and worth having: this
/// annotation appears on ~70 write actions, and at that density
/// <c>[RequirePlanner]</c> versus <c>[Authorize(Policy = AccessPolicies.Planner)]</c>
/// is the difference between a surface you can audit by eye and one you skim.
/// </para>
/// <para>
/// They are still ordinary <c>AuthorizeAttribute</c>s pointing at a policy, so
/// nothing about the evaluation changes — in particular none of them is
/// <c>[Authorize(Roles = ...)]</c>, which would read a claim the identity provider
/// controls.
/// </para>
/// <para>
/// <b>Reads carry no attribute.</b> The fallback policy is <see cref="RequireViewerAttribute"/>'s
/// policy, so every un-annotated endpoint already demands membership; annotating
/// each of the 44 GETs would add noise without adding a rule. Annotate a read only
/// when it needs MORE than Viewer.
/// </para>
/// </remarks>
public sealed class RequireViewerAttribute() : AuthorizeAttribute(AccessPolicies.Viewer);

/// <inheritdoc cref="RequireViewerAttribute"/>
public sealed class RequirePlannerAttribute() : AuthorizeAttribute(AccessPolicies.Planner);

/// <inheritdoc cref="RequireViewerAttribute"/>
public sealed class RequireOwnerAttribute() : AuthorizeAttribute(AccessPolicies.Owner);
