namespace ResourcePulse.Domain.Signals;

// The queue order (ADR-0032 §11). Pure, in the domain, like CapacityCalculator
// and LoadCalculator — and unlike the sustainability verdict, which stayed
// client-side (project-gap.md §★★), because here the server holds the HISTORY
// with which to explain the order.
//
//   Zone      Overdue < Frozen < Slushy < Liquid, null last
//   Tier      Breach < Hygiene < Slack
//   Committed the hard-committed first
//   Kind      ← see below
//   Magnitude descending
//   FirstDetectedAt  (stable tiebreak)
//
// Kind precedes Magnitude ON PURPOSE. Magnitudes are not commensurable across
// kinds: gap hours, percentage points over the overload floor and cardinalities
// are different scales. The prototype papers over this by multiplying the
// overshoot by four to make it look like hours; here comparisons only ever happen
// WITHIN a kind, which is the only place they mean anything.
public static class SignalRanking
{
    // null zone sorts after every real one: absence of a deadline is absence of
    // urgency, not maximum urgency.
    private const int NoZoneRank = int.MaxValue;

    public static int ZoneRank(SignalZone? zone) => zone is SignalZone z ? (int)z : NoZoneRank;

    public static IReadOnlyList<PlanSignal> Rank(IEnumerable<PlanSignal> signals) =>
        signals
            .OrderBy(s => ZoneRank(s.Zone))
            .ThenBy(s => (int)s.Tier)
            .ThenByDescending(s => s.HardCommitted)
            .ThenBy(s => (int)s.Kind)
            .ThenByDescending(s => s.Magnitude)
            .ThenBy(s => s.FirstDetectedAt)
            .ThenBy(s => s.Id)
            .ToList();

    // The queue budget is a DECLARED CONSTANT, not an org-level knob (ADR-0032
    // §12). It exists to force triage: an organization that raises it to 50 has
    // rebuilt the infinite list and dismantled the product. The value can change
    // — by amending the ADR, not by turning a dial per tenant.
    public const int QueueBudget = 7;
}
