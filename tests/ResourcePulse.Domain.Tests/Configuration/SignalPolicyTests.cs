using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Tests.Configuration;

public class SignalPolicyTests
{
    [Fact]
    public void Default_Is90DaysOfFeedDepthAndTwoWeeksOfLeadTime()
    {
        var policy = SignalPolicy.CreateDefault();

        policy.ResolvedRetentionDays.Should().Be(90);
        policy.DecisionLeadTime.Value.Should().Be(2);
        policy.DecisionLeadTime.Unit.Should().Be(DurationUnit.Weeks);
    }

    [Fact]
    public void DerivedDeadline_IsThePlannedStartMinusTheLeadTime()
    {
        // ADR-0033 §3: this is what gives EVERY demand a deadline at zero
        // friction. A nullable field nobody fills would leave the queue with
        // nothing to rank on.
        var policy = SignalPolicy.CreateDefault();

        policy.DeriveDeadline(new DateOnly(2026, 7, 20)).Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void DerivedDeadline_MatchesTheFrozenBoundary_WithTheDefaults()
    {
        // The readable property the two defaults buy: with a 2-week lead time and
        // a 2-week frozen horizon, the decision on a demand falls due exactly when
        // the work enters the frozen zone.
        var today = new DateOnly(2026, 6, 24);
        var fence = TimeFenceConfiguration.CreateDefault().ComputeBoundaries(today);
        var workStarts = new DateOnly(2026, 7, 22);

        var deadline = SignalPolicy.CreateDefault().DeriveDeadline(workStarts)!.Value;

        deadline.Should().Be(new DateOnly(2026, 7, 8));
        deadline.Should().Be(fence.FrozenUntil);
    }

    [Theory]
    [InlineData(3, DurationUnit.Days, 2026, 7, 17)]
    [InlineData(6, DurationUnit.Weeks, 2026, 6, 8)]
    [InlineData(2, DurationUnit.Months, 2026, 5, 20)]
    public void LeadTime_SubtractsInItsOwnUnit(int value, DurationUnit unit, int y, int m, int d)
    {
        var policy = SignalPolicy.Create(Guid.NewGuid(), 90, Duration.Of(value, unit));

        policy.DeriveDeadline(new DateOnly(2026, 7, 20)).Should().Be(new DateOnly(y, m, d));
    }

    [Fact]
    public void NodeWithoutAPlannedStart_YieldsNoDeadline()
    {
        // Not "no urgency": it is a defect in the model, surfaced as the
        // DemandOnUndatedNode hygiene kind (ADR-0033 §9).
        SignalPolicy.CreateDefault().DeriveDeadline(null).Should().BeNull();
    }

    [Fact]
    public void RetentionBelowOneDay_IsRejected()
    {
        var act = () => SignalPolicy.Create(Guid.NewGuid(), 0, Duration.Of(2, DurationUnit.Weeks));
        act.Should().Throw<DomainException>().WithMessage("*at least one day*");
    }

    [Fact]
    public void Replace_UpdatesBothValues()
    {
        var policy = SignalPolicy.CreateDefault();

        policy.Replace(30, Duration.Of(6, DurationUnit.Weeks));

        policy.ResolvedRetentionDays.Should().Be(30);
        policy.DecisionLeadTime.Value.Should().Be(6);
    }
}
