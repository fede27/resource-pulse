using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Signals;

// A triage-able condition on the plan, PERSISTED (ADR-0032).
//
// Not a recomputed view and not a cache. Two things force the persistence, and
// neither is the acknowledgement:
//   - "unseen" needs a FirstDetectedAt, i.e. a row per detected condition;
//   - the four verbs of "Cosa è cambiato" are transitions of this row.
// The identity problem is not solved by persisting — persisting PRESUPPOSES it:
// a detector that writes rows must decide whether what it just measured matches
// an open row, which is exactly the natural key.
//
// Named PlanSignal, not Signal: SignalCards already exists in the frontend as the
// shared page-header component and they are different things. Exception is out —
// it collides with System.Exception in every C# file.
//
// LIFECYCLE — two orthogonal axes, NOT a chain (§3):
//   Detection { Live, Resolved }      written only by the detector
//   acknowledgement { none, accepted } written only by a person
// An acknowledged signal stays in the queue, labelled as an assumed risk. And
// Resolved is always a detector conclusion: the dashboard's inline confirm
// mutates the plan and lets the next pass observe the condition is gone.
//
// IDENTITY (§2): (Kind, SubjectId?) unique AMONG LIVE ROWS. When a condition
// returns after being resolved, a NEW row is created — which is what makes
// "an acknowledgement does not survive resolution" structural rather than a rule
// somebody has to enforce, and keeps FirstDetectedAt unambiguous.
public sealed class PlanSignal : Entity<Guid>, IAuditable
{
    public SignalKind Kind { get; private set; }

    // Null for Aggregate kinds. At the DB level the partial unique index on live
    // rows is declared NULLS NOT DISTINCT — without it two live aggregate rows of
    // the same kind would not collide and reconciliation would silently duplicate.
    public Guid? SubjectId { get; private set; }

    public SignalDetection Detection { get; private set; } = SignalDetection.Live;

    // ── Observation (mutable; the identity is not) ───────────────────────
    public DateOnly? DeadlineAt { get; private set; }
    public SignalZone? Zone { get; private set; }
    public decimal Magnitude { get; private set; }
    public bool HardCommitted { get; private set; }

    private List<Guid> _touchedRootProjectIds = [];
    // The uniform scope rule (§8): "my projects" filters on this, so a person-
    // subject signal is still visible to whoever leads the projects it touches.
    public IReadOnlyList<Guid> TouchedRootProjectIds => _touchedRootProjectIds.AsReadOnly();

    private List<Guid> _memberSubjectIds = [];
    // Aggregate kinds only. Stored as an unordered payload on purpose: making it
    // awkward to sort is what keeps the anti-ranking constraint structural
    // instead of a note in a comment (§7).
    public IReadOnlyList<Guid> MemberSubjectIds => _memberSubjectIds.AsReadOnly();

    // ── Change tracking (the feed, §6) ───────────────────────────────────
    public DateTimeOffset FirstDetectedAt { get; private set; }
    public DateTimeOffset LastObservedAt { get; private set; }
    public DateTimeOffset? LastChangedAt { get; private set; }
    public SignalChange LastChange { get; private set; } = SignalChange.None;
    public decimal? PreviousMagnitude { get; private set; }
    public SignalZone? PreviousZone { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    // ── Acknowledgement (append-only, §4) ────────────────────────────────
    private readonly List<SignalAcknowledgement> _acknowledgements = [];
    public IReadOnlyList<SignalAcknowledgement> Acknowledgements =>
        _acknowledgements.OrderBy(a => a.Sequence).ToList();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Derived (never stored) ───────────────────────────────────────────
    public SignalTier Tier => SignalKinds.TierOf(Kind);
    public SignalShape Shape => SignalKinds.ShapeOf(Kind);
    public bool IsLive => Detection == SignalDetection.Live;

    public SignalAcknowledgement? CurrentAcknowledgement
    {
        get
        {
            var last = _acknowledgements.OrderBy(a => a.Sequence).LastOrDefault();
            return last?.Action == AcknowledgementAction.Accept ? last : null;
        }
    }

    public bool IsAcknowledged => CurrentAcknowledgement is not null;

    private PlanSignal() { }

    // ── Detection ────────────────────────────────────────────────────────

    public static PlanSignal Detect(
        SignalKind kind,
        Guid? subjectId,
        SignalObservation observation,
        DateTimeOffset detectedAt)
    {
        ArgumentNullException.ThrowIfNull(observation);
        SignalKinds.AssertKnown(kind);

        if (SignalKinds.RequiresSubject(kind))
        {
            if (subjectId is not Guid s || s == Guid.Empty)
                throw new DomainException($"Signal kind '{kind}' is per-subject and requires a subject id.");
        }
        else if (subjectId is not null)
        {
            throw new DomainException($"Signal kind '{kind}' is aggregated and must not carry a subject id.");
        }

        var signal = new PlanSignal
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            SubjectId = subjectId,
            Detection = SignalDetection.Live,
            FirstDetectedAt = detectedAt,
            LastObservedAt = detectedAt,
            LastChangedAt = detectedAt,
            LastChange = SignalChange.Created
        };
        signal.Apply(observation);
        return signal;
    }

    // Records a fresh measurement of the SAME condition and reports what — if
    // anything — worsened. Only worsening is reported: the data is always
    // updated, but a shrinking magnitude and a relaxing zone stay out of the feed
    // (§6). Pinning the deadline at first detection instead would have been
    // dishonest: a breach sliding forward would keep claiming a false deadline
    // and stay Overdue for good.
    public SignalChange Observe(SignalObservation observation, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Detection == SignalDetection.Resolved)
            throw new DomainException("A resolved signal is a historical record and cannot be re-observed. Detect a new one.");

        var change = SignalChange.None;
        if (observation.Magnitude > Magnitude) change |= SignalChange.MagnitudeWorsened;
        if (SignalZones.IsWorsening(Zone, observation.Zone)) change |= SignalChange.ZoneWorsened;

        if (change != SignalChange.None)
        {
            PreviousMagnitude = Magnitude;
            PreviousZone = Zone;
            LastChangedAt = observedAt;
            LastChange = change;
        }

        Apply(observation);
        LastObservedAt = observedAt;
        return change;
    }

    // Written ONLY by the detector, when the condition is no longer observed.
    public void Resolve(DateTimeOffset resolvedAt)
    {
        if (Detection == SignalDetection.Resolved) return; // idempotent: re-sweeps are normal

        Detection = SignalDetection.Resolved;
        ResolvedAt = resolvedAt;
        LastChangedAt = resolvedAt;
        LastChange = SignalChange.Resolved;
    }

    // ── Human decision ───────────────────────────────────────────────────

    // "Ho deciso che è accettabile." Does NOT hide the signal: it stays in the
    // queue as an assumed risk, distinct from resolved.
    public void Acknowledge(string by, string? reason, DateTimeOffset at)
    {
        if (Detection == SignalDetection.Resolved)
            throw new DomainException("A resolved signal carries no risk to accept.");
        if (IsAcknowledged) return; // idempotent

        Append(AcknowledgementAction.Accept, at, by, reason);
    }

    public void Reopen(string by, DateTimeOffset at)
    {
        if (!IsAcknowledged) return; // idempotent

        Append(AcknowledgementAction.Reopen, at, by, reason: null);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private void Append(AcknowledgementAction action, DateTimeOffset at, string by, string? reason)
    {
        var sequence = _acknowledgements.Count == 0 ? 1 : _acknowledgements.Max(a => a.Sequence) + 1;
        _acknowledgements.Add(SignalAcknowledgement.Create(sequence, action, at, by, reason));
    }

    private void Apply(SignalObservation observation)
    {
        DeadlineAt = observation.DeadlineAt;
        Zone = observation.Zone;
        Magnitude = observation.Magnitude;
        HardCommitted = observation.HardCommitted;
        _touchedRootProjectIds = [.. observation.TouchedRootProjectIds];
        _memberSubjectIds = [.. observation.MemberSubjectIds];
    }
}
