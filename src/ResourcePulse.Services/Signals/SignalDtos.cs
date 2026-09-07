using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Services.Signals;

// Binary by decision: "my projects" means the ones I LEAD
// (ProjectNode.LeadResourceId), and every signal — including the ones whose
// subject is a person — is scoped through the root projects it touches
// (ADR-0032 §8). The prototype's third "my team" scope is gone with it.
public enum SignalScope
{
    Mine = 0,
    All = 1
}

// Where the row's verb takes you, with the context preloaded. The dashboard's
// admission rule is that no element exists without a verb AND a deep link, so
// this is not decoration.
public sealed class SignalLinkDto
{
    public Guid? RootProjectId { get; init; }
    public Guid? ProjectNodeId { get; init; }
    public Guid? DemandId { get; init; }
    public Guid? AllocationId { get; init; }
    public Guid? ResourceId { get; init; }
    public Guid? RoleId { get; init; }
}

// One triage row.
//
// NOTE ON TEXT: this DTO carries the PARTS of a title, never a composed sentence.
// Translation lives in the frontend locales; a server-side string would be
// untranslatable and would put presentation in the read model.
public sealed class SignalDto
{
    public Guid Id { get; init; }
    public SignalKind Kind { get; init; }
    public SignalTier Tier { get; init; }
    public SignalShape Shape { get; init; }

    public Guid? SubjectId { get; init; }

    public SignalZone? Zone { get; init; }
    public DateOnly? DeadlineAt { get; init; }

    // A quantity for Subject signals (gap hours, points over the overload floor);
    // a CARDINALITY for Aggregate ones. Never comparable across kinds.
    public decimal Magnitude { get; init; }
    public bool HardCommitted { get; init; }

    // Resolved labels (ADR-0024 pattern), batch-resolved. Which ones are present
    // depends on the kind; the client renders only what it needs.
    public string? RoleName { get; init; }
    public string? ResourceName { get; init; }
    public string? RootProjectName { get; init; }
    public string? OwnerName { get; init; }

    // Aggregate kinds: how many occurrences, and their names — sorted
    // ALPHABETICALLY and carrying no figure, so the list is structurally
    // incapable of reading as a ranking (ADR-0032 §7).
    public IReadOnlyList<string> MemberNames { get; init; } = [];

    public SignalLinkDto Link { get; init; } = new();

    public DateTimeOffset FirstDetectedAt { get; init; }
    public DateTimeOffset LastObservedAt { get; init; }

    // Appeared after this user last looked (ADR-0032 §5). Per-user, derived from
    // one marker row — not a per-(user, signal) table.
    public bool IsUnseen { get; init; }

    // Acknowledged ≠ resolved: the row STAYS in the queue, labelled as an assumed
    // risk. Both are surfaced so the client never conflates them.
    public bool IsAcknowledged { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? AcknowledgedReason { get; init; }

    // Explains why the row sits where it does: the decomposition the expander
    // shows instead of a recommended move (ADR-0032, "Alternative considerate").
    public IReadOnlyList<SignalContributionDto> Contributions { get; init; } = [];
}

// A fact about what makes up the signal — never a suggestion. For an Overcommit:
// the blocks that contribute, which the load profile already decomposes by root
// project. Deliberately NOT a candidate move: choosing who covers is a staffing
// gesture and belongs on the page that shows the context.
public sealed class SignalContributionDto
{
    public Guid RootProjectId { get; init; }
    public string RootProjectName { get; init; } = string.Empty;
    public decimal Percent { get; init; }
}

// One entry of the "Cosa è cambiato" feed. Not a mutation log: these are
// transitions of the signal itself, which is why a delta on a DERIVED value
// ("da 105% a 120%") is expressible at all (ADR-0032 §6).
public sealed class SignalChangeDto
{
    public Guid SignalId { get; init; }
    public SignalKind Kind { get; init; }
    public SignalChange Change { get; init; }
    public DateTimeOffset At { get; init; }

    public string? RoleName { get; init; }
    public string? ResourceName { get; init; }
    public string? RootProjectName { get; init; }

    // For "Peggiorato": the two figures the sentence compares.
    public decimal Magnitude { get; init; }
    public decimal? PreviousMagnitude { get; init; }

    // For "Attraversato": the zone it moved into, and the one it left.
    public SignalZone? Zone { get; init; }
    public SignalZone? PreviousZone { get; init; }

    public SignalLinkDto Link { get; init; } = new();
}

// Whether we have looked, and when (ADR-0032 §10). An empty queue is only
// "il piano tiene" if LastSweptAt is set and recent; the client renders three
// distinct states from this, not two.
public sealed class SignalSweepDto
{
    // Null ⇒ the detector has never run on this tenant. Not a missing value to be
    // defaulted away: it is the state.
    public DateTimeOffset? LastSweptAt { get; init; }
    public int LiveCount { get; init; }

    // How old a pass may get before the page should say so. Echoed from the
    // worker's configured cadence so no client hard-codes a tolerance.
    public int StaleAfterHours { get; init; }

    // Since when the change feed is being shown — this user's previous visit.
    public DateTimeOffset? LastVisitedAt { get; init; }

    // The declared constant (ADR-0032 §12), echoed read-only so the page can say
    // "7 di 40" without hard-coding 7.
    public int QueueBudget { get; init; }
}

public sealed class AcknowledgeSignalDto
{
    // Optional, exactly as the dialog offers it: "Motivo (opzionale)".
    public string? Reason { get; init; }
}
