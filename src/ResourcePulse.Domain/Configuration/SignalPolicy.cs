using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Org-level singleton, the FIFTH (ADR-0032 §12). Its own aggregate rather than a
// field on an existing one: separate lifecycle, and ADR-0020's no-mega-blob rule.
//
// CONFIGURABLE — exactly two things, both of which genuinely vary by organization:
//   - how deep the change feed goes (resolved signals ARE the history, so
//     retention is depth, not cleanup);
//   - how much notice a staffing decision needs.
//
// CONSTANT (declared, not config) — and the list matters as much as the fields:
//   - the QUEUE BUDGET (SignalRanking.QueueBudget). It exists to FORCE triage;
//     an org that raises it to 50 has rebuilt the infinite list. Textbook case of
//     "don't turn every constant into a dial" (ADR-0020 §1).
//   - the under-band and overload thresholds — already in LoadBandConfiguration.
//     Duplicating them is the explicit anti-pattern.
//   - which kinds are enabled. If an org can switch off Overcommit, the dashboard
//     lies.
//   - the sweep cadence: operational, not organizational — worker configuration.
public sealed class SignalPolicy : Entity<Guid>, IAuditable
{
    // One row per tenant, enforced by a unique index on tenant_id (ADR-0029).

    // How long a resolved signal is kept. Not housekeeping: resolved signals are
    // the substrate of "Cosa è cambiato" (ADR-0032 §6), so this is the depth of
    // the feed.
    public int ResolvedRetentionDays { get; private set; }

    // How early a staffing decision falls due relative to the work starting —
    // the derived deadline is node.PlannedStart − DecisionLeadTime (ADR-0033 §3).
    //
    // ONE lead time, not one per role or per project type. Not laziness: the
    // notice a position really needs depends on the POSITION ("this cloud
    // engineer in two weeks, this senior PM no"), so a per-role table would still
    // be coarse while adding a dial per catalogue row. The variance is absorbed
    // where it actually lives — Demand.DecideBy, the per-demand override.
    public Duration DecisionLeadTime { get; private set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private SignalPolicy() { }

    // Opinionated default: 90 days of feed depth, 2 weeks of lead time.
    //
    // The lead time deliberately matches the default frozen horizon, which buys a
    // readable property: the decision on a demand falls due exactly when the work
    // enters the frozen zone.
    public static SignalPolicy CreateDefault() =>
        Create(Guid.NewGuid(), 90, Duration.Of(2, DurationUnit.Weeks));

    public static SignalPolicy Create(Guid id, int resolvedRetentionDays, Duration decisionLeadTime)
    {
        var policy = new SignalPolicy { Id = id };
        policy.SetValues(resolvedRetentionDays, decisionLeadTime);
        return policy;
    }

    public void Replace(int resolvedRetentionDays, Duration decisionLeadTime) =>
        SetValues(resolvedRetentionDays, decisionLeadTime);

    // The derived deadline of ADR-0033 §3. Null when the node has no planned
    // start — which is not "no urgency" but a defect in the model, surfaced as
    // the DemandOnUndatedNode hygiene kind (§9).
    public DateOnly? DeriveDeadline(DateOnly? plannedStart) =>
        plannedStart is DateOnly start ? SubtractLeadTime(start) : null;

    private DateOnly SubtractLeadTime(DateOnly start) => DecisionLeadTime.Unit switch
    {
        DurationUnit.Days => start.AddDays(-DecisionLeadTime.Value),
        DurationUnit.Weeks => start.AddDays(-DecisionLeadTime.Value * 7),
        DurationUnit.Months => start.AddMonths(-DecisionLeadTime.Value),
        _ => throw new DomainException($"Invalid duration unit '{DecisionLeadTime.Unit}'.")
    };

    private void SetValues(int resolvedRetentionDays, Duration decisionLeadTime)
    {
        ArgumentNullException.ThrowIfNull(decisionLeadTime);
        if (resolvedRetentionDays < 1)
            throw new DomainException("Resolved-signal retention must be at least one day.");

        ResolvedRetentionDays = resolvedRetentionDays;
        DecisionLeadTime = decisionLeadTime;
    }
}
