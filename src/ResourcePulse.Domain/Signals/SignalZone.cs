namespace ResourcePulse.Domain.Signals;

// The urgency zone of a signal's DEADLINE (ADR-0033). Not the zone of the work:
// "proximity to the fence" means how much time is left to decide, which is also
// the better reading of a planning fence — the frozen zone is where you can no
// longer change things, so a decision falling due inside it is urgent by
// definition.
//
// Overdue is DERIVED (deadline < today), not a configured boundary:
// TimeFenceConfiguration keeps its two horizons plus implicit liquid, untouched.
// Clamping past dates to today (as the prototype does) would flatten "you are
// late" onto "you are in the frozen zone" — different things that must rank
// differently.
//
// The enum order IS the ranking. A signal with NO deadline carries a null zone
// and ranks after every zoned one: absence of a deadline is absence of urgency,
// not maximum urgency.
public enum SignalZone
{
    Overdue = 0,
    Frozen = 1,
    Slushy = 2,
    Liquid = 3
}
