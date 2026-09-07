using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Signals;

// Pure resolution of a signal's urgency zone from its DEADLINE (ADR-0033).
//
// Two things this is NOT:
//   - it is not the zone of the work. The deadline is "how long you have left to
//     decide" (Gap) or "after this it has happened" (Overcommit), never a work
//     window. ADR-0027 Decision 4 stands: the gap has no span.
//   - it does not clamp past deadlines to today. A missed deadline outranks a
//     frozen one; flattening them (as the prototype does) loses the distinction
//     the ranking exists to make.
public static class SignalZones
{
    // Null deadline ⇒ null zone: hygiene and slack have no deadline, and saying
    // so is more honest than the prototype's hardcoded "frozen".
    public static SignalZone? Resolve(DateOnly? deadline, DateOnly today, FenceBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        if (deadline is not DateOnly d) return null;
        if (d < today) return SignalZone.Overdue;

        return boundaries.ZoneFor(d) switch
        {
            FenceZone.Frozen => SignalZone.Frozen,
            FenceZone.Slushy => SignalZone.Slushy,
            FenceZone.Liquid => SignalZone.Liquid,
            var z => throw new DomainException($"Unmapped fence zone '{z}'.")
        };
    }

    // Admission (ADR-0033 §8): only signals whose deadline falls inside the
    // committing horizon [today, slushyUntil] enter the queue — plus those with
    // NO deadline, which enter on tier alone.
    public static bool IsInCommittingHorizon(SignalZone? zone) =>
        zone is null or SignalZone.Overdue or SignalZone.Frozen or SignalZone.Slushy;

    // A zone change only counts as a change when it moves TOWARD urgency
    // (ADR-0032 §6). One rule covers both cases we care about: the tentative
    // entering the frozen zone because the fence rolled speaks; the overcommit
    // sliding two weeks further out stays silent.
    public static bool IsWorsening(SignalZone? previous, SignalZone? current)
    {
        if (current is null) return false;          // losing a deadline is not urgency
        if (previous is null) return true;          // gaining one is
        return current < previous;                  // enum order IS the ranking
    }
}
