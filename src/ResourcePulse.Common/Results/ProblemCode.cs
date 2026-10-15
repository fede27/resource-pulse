namespace ResourcePulse.Common.Results;

/// <summary>
/// The refusals a client can tell apart (ADR-0037): a stable name for WHICH rule
/// said no, so the client can branch on it and say it in the user's language.
/// </summary>
/// <remarks>
/// <para>
/// A code travels as the ProblemDetails <c>type</c>,
/// <c>urn:resourcepulse:problem:{slug}</c>, next to an <c>args</c> extension that
/// carries the PIECES of the refusal (a level, a count, a date) in their wire
/// format — never a sentence. The English <c>detail</c> stays, for logs and as
/// the fallback of a client that does not know the code yet.
/// </para>
/// <para>
/// The enum is the catalogue: the OpenAPI document exports it as a string enum of
/// slugs, the SPA's generated client turns it into a type, and the locales are
/// keyed by it — so a code added here without a translation does not compile on
/// the client. Rename a member and its slug changes with it: that is a breaking
/// change of the contract, not a refactoring.
/// </para>
/// <para>
/// A code is optional. An error without one is rendered as before, and is
/// converted when a client needs to tell it apart — not in bulk.
/// </para>
/// </remarks>
public enum ProblemCode
{
    /// <summary>I6: Hard on a root whose commitment level the policy does not count as hard.
    /// Args: <c>level</c> (CommitmentLevel or null), <c>required</c> (CommitmentLevel[]).</summary>
    HardRequiresCommitment,

    /// <summary>A commitment downgrade across the I6 threshold would demote Hard coverage
    /// and was not confirmed. Args: <c>from</c>, <c>to</c> (CommitmentLevel), <c>count</c>.</summary>
    CommitmentDowngradeDemotesHard,

    // ── The plan (envelope) ─────────────────────────────────────────────────

    /// <summary>I4: the root project is closed or cancelled and its plan no longer changes.
    /// Args: <c>project</c> (name), <c>status</c> (ProjectStatus).</summary>
    ProjectRootClosed,

    /// <summary>I3: the person is inactive and cannot be given coverage. Args: <c>resource</c> (name).</summary>
    ResourceInactive,

    /// <summary>An edge would follow its node, and the node has no planned date on that side
    /// (ADR-0035 §2). Args: <c>node</c> (name), <c>edge</c> (BoundaryEdge).</summary>
    NodeHasNoPlannedDate,

    /// <summary>A node move would clear a planned date that boundaries follow (ADR-0035 §5).
    /// Args: <c>node</c> (name), <c>edge</c> (BoundaryEdge), <c>count</c> (boundaries).</summary>
    PlannedDateFollowed,

    /// <summary>Following its node's dates would invert a block — the node shrank past the
    /// block's pinned edge (ADR-0035 §5; also after a retarget). Nothing is auto-shortened.
    /// Args: <c>node</c> (name), <c>resource</c> (name), <c>from</c>, <c>to</c> (the block's dates).</summary>
    FollowWouldInvertCoverage,

    /// <summary>The person has no capacity in the window, so hours cannot become a percent.
    /// Args: <c>resource</c> (name), <c>from</c>, <c>to</c>.</summary>
    ZeroCapacityInWindow,

    /// <summary>The hours asked resolve above the percent ceiling in that window.
    /// Args: <c>percent</c> (resolved), <c>cap</c>.</summary>
    HoursAbovePercentCap,

    /// <summary>A demand that still has coverage cannot be deleted. Args: <c>count</c> (coverages).</summary>
    DemandHasCoverage,

    // ── The anagrafica ──────────────────────────────────────────────────────

    /// <summary>Boundaries follow this node's planned dates, so moving them is a plan mutation
    /// (replanNode / moveSubtree), not an anagrafica edit. Args: <c>count</c> (boundaries),
    /// <c>operation</c> ("replan" | "rollUp").</summary>
    NodeHasFollowers,

    /// <summary>A node still carries structure or plan and cannot be deleted. Checked in order —
    /// children first, then the plan, then imposed dates — and the args carry only the counts of the
    /// check that refused: <c>node</c> (name) plus <c>children</c>; or <c>demands</c> and
    /// <c>coverages</c>; or <c>constraints</c>.</summary>
    NodeInUse,

    /// <summary>A role still named by people or demands cannot be deleted.
    /// Args: <c>role</c> (name), <c>resources</c>, <c>demands</c> (counts).</summary>
    RoleInUse,

    /// <summary>A team that still has members cannot be deleted. Args: <c>team</c> (name), <c>resources</c> (count).</summary>
    TeamInUse,
}

public static class ProblemCodes
{
    public const string TypePrefix = "urn:resourcepulse:problem:";

    /// <summary>The wire name of a code: its member name in kebab case.</summary>
    public static string Slug(ProblemCode code)
    {
        var name = code.ToString();
        var slug = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) slug.Append('-');
                slug.Append(char.ToLowerInvariant(c));
            }
            else slug.Append(c);
        }
        return slug.ToString();
    }

    /// <summary>The ProblemDetails <c>type</c> a code travels as.</summary>
    public static string TypeOf(ProblemCode code) => TypePrefix + Slug(code);
}
