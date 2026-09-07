using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Signals;

// What the detector measured about a condition on one pass (ADR-0032 §6).
//
// The zone is computed HERE, from the deadline and the fence boundaries, and
// never accepted from a caller: one constructor means the deadline and the zone
// cannot diverge — the ranking has to remain explainable from the data it
// carries.
public sealed record SignalObservation
{
    public DateOnly? DeadlineAt { get; private init; }
    public SignalZone? Zone { get; private init; }

    // A quantity for Subject signals (gap hours, points over the overload floor),
    // a CARDINALITY for Aggregate ones. Never comparable across kinds — which is
    // why the ranking sorts by Kind before Magnitude (ADR-0032 §11).
    public decimal Magnitude { get; private init; }

    // From CommitmentPolicy.IsHardCommitted on the root project (ADR-0020): the
    // same threshold that gates I6, never a second copy of it.
    public bool HardCommitted { get; private init; }

    // The root projects this condition touches — the UNIFORM scope rule
    // (ADR-0032 §8). An Overcommit has no project of its own, but it touches the
    // ones the person covers, so "my projects" can still surface it.
    public IReadOnlyList<Guid> TouchedRootProjectIds { get; private init; } = [];

    // Aggregate signals only: the occurrences behind the count. Unordered by
    // contract — see PlanSignal.MemberSubjectIds.
    public IReadOnlyList<Guid> MemberSubjectIds { get; private init; } = [];

    public static SignalObservation For(
        DateOnly? deadline,
        DateOnly today,
        FenceBoundaries boundaries,
        decimal magnitude,
        bool hardCommitted = false,
        IReadOnlyList<Guid>? touchedRootProjectIds = null,
        IReadOnlyList<Guid>? memberSubjectIds = null)
    {
        if (magnitude < 0)
            throw new DomainException("Signal magnitude must not be negative.");

        return new SignalObservation
        {
            DeadlineAt = deadline,
            Zone = SignalZones.Resolve(deadline, today, boundaries),
            Magnitude = magnitude,
            HardCommitted = hardCommitted,
            TouchedRootProjectIds = Distinct(touchedRootProjectIds),
            MemberSubjectIds = Distinct(memberSubjectIds)
        };
    }

    private static IReadOnlyList<Guid> Distinct(IReadOnlyList<Guid>? ids) =>
        ids is null || ids.Count == 0 ? [] : ids.Where(id => id != Guid.Empty).Distinct().ToList();
}
