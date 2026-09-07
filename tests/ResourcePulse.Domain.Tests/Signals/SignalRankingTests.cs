using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class SignalRankingTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);
    private static readonly DateTimeOffset T0 = new(2026, 6, 24, 6, 0, 0, TimeSpan.Zero);

    private static FenceBoundaries Boundaries() =>
        TimeFenceConfiguration
            .Create(Guid.NewGuid(), Duration.Of(2, DurationUnit.Weeks), Duration.Of(2, DurationUnit.Months))
            .ComputeBoundaries(Today);

    private static PlanSignal Signal(
        SignalKind kind,
        DateOnly? deadline = null,
        decimal magnitude = 1m,
        bool hardCommitted = false) =>
        PlanSignal.Detect(
            kind,
            SignalKinds.RequiresSubject(kind) ? Guid.NewGuid() : null,
            SignalObservation.For(deadline, Today, Boundaries(), magnitude, hardCommitted),
            T0);

    [Fact]
    public void ZoneDominates_AndOverdueLeads()
    {
        var liquid = Signal(SignalKind.Gap, Today.AddDays(90), magnitude: 500m);
        var frozen = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 1m);
        var overdue = Signal(SignalKind.Gap, Today.AddDays(-5), magnitude: 1m);

        SignalRanking.Rank([liquid, frozen, overdue])
            .Should().Equal(overdue, frozen, liquid);
    }

    [Fact]
    public void NoDeadline_RanksLast_BecauseAbsenceOfUrgencyIsNotUrgency()
    {
        var hygiene = Signal(SignalKind.NoDefaultCalendar);            // no deadline
        var liquidGap = Signal(SignalKind.Gap, Today.AddDays(120));

        SignalRanking.Rank([hygiene, liquidGap]).Should().Equal(liquidGap, hygiene);
    }

    [Fact]
    public void WithinAZone_TierOrdersBreachBeforeHygieneBeforeSlack()
    {
        // Without the tier an informational row of magnitude zero could outrank a
        // frozen breach.
        var slack = Signal(SignalKind.UnderBand);
        var hygiene = Signal(SignalKind.DemandOnClosedRoot);
        var breach = Signal(SignalKind.Overcommit);

        SignalRanking.Rank([slack, hygiene, breach]).Should().Equal(breach, hygiene, slack);
    }

    [Fact]
    public void HardCommittedOutranksProposed_AtEqualZoneAndTier()
    {
        var proposed = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 300m, hardCommitted: false);
        var committed = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 10m, hardCommitted: true);

        SignalRanking.Rank([proposed, committed]).Should().Equal(committed, proposed);
    }

    [Fact]
    public void MagnitudesAreNeverComparedAcrossKinds()
    {
        // THE point of ordering by Kind before Magnitude (ADR-0032 §11): 40 gap
        // hours and 900 percentage points are not on the same scale, and the
        // prototype's fix — multiplying the overshoot by four to make it look like
        // hours — is arithmetic dressed as meaning. Kind wins, and Gap (1) sorts
        // before Overcommit (3).
        var hugeOvercommit = Signal(SignalKind.Overcommit, Today.AddDays(2), magnitude: 900m);
        var smallGap = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 40m);

        SignalRanking.Rank([hugeOvercommit, smallGap]).Should().Equal(smallGap, hugeOvercommit);
    }

    [Fact]
    public void WithinAKind_MagnitudeOrdersDescending()
    {
        var small = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 8m);
        var large = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 80m);

        SignalRanking.Rank([small, large]).Should().Equal(large, small);
    }

    [Fact]
    public void RankIsStable_ForFullyTiedSignals()
    {
        var a = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 10m);
        var b = Signal(SignalKind.Gap, Today.AddDays(2), magnitude: 10m);

        SignalRanking.Rank([a, b]).Should().Equal(SignalRanking.Rank([a, b]));
        SignalRanking.Rank([b, a]).Should().Equal(SignalRanking.Rank([a, b]));
    }

    [Fact]
    public void QueueBudgetIsSeven_AndIsADeclaredConstant() =>
        SignalRanking.QueueBudget.Should().Be(7);
}
