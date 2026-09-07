namespace ResourcePulse.Domain.Signals;

// What changed about a signal on one detector pass — the substrate of the
// "Cosa è cambiato" feed (ADR-0032 §6). The four flags ARE the four verbs of the
// prototype, which is not a coincidence: they are transitions of this aggregate,
// not mutations of the plan. "Luca went from 105% to 120%" is a delta on a
// DERIVED value; no mutation log would produce it.
//
// Only WORSENING counts. A magnitude that shrinks and a zone that relaxes update
// the data and stay out of the feed.
[Flags]
public enum SignalChange
{
    None = 0,

    // Nuovo
    Created = 1,

    // Peggiorato
    MagnitudeWorsened = 2,

    // Attraversato
    ZoneWorsened = 4,

    // Risolto
    Resolved = 8
}
