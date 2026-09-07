using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class SignalKindsTests
{
    [Theory]
    [InlineData(SignalKind.Gap, SignalTier.Breach)]
    [InlineData(SignalKind.TentativeInFrozen, SignalTier.Breach)]
    [InlineData(SignalKind.Overcommit, SignalTier.Breach)]
    [InlineData(SignalKind.DemandOnClosedRoot, SignalTier.Hygiene)]
    [InlineData(SignalKind.CoverageOutOfWindow, SignalTier.Hygiene)]
    [InlineData(SignalKind.DemandOnUndatedNode, SignalTier.Hygiene)]
    [InlineData(SignalKind.NoDefaultCalendar, SignalTier.Hygiene)]
    [InlineData(SignalKind.InactiveWithCoverage, SignalTier.Hygiene)]
    [InlineData(SignalKind.UnderBand, SignalTier.Slack)]
    public void TierOf_ClassifiesEveryKind(SignalKind kind, SignalTier expected) =>
        SignalKinds.TierOf(kind).Should().Be(expected);

    [Fact]
    public void EveryDefinedKind_IsClassified()
    {
        // The guard that matters: adding a kind to the enum without classifying it
        // must fail here rather than throw at the first sweep on a real tenant.
        foreach (var kind in Enum.GetValues<SignalKind>())
        {
            var act = () => SignalKinds.AssertKnown(kind);
            act.Should().NotThrow($"'{kind}' must declare a tier");
        }
    }

    [Fact]
    public void BreachesAreNamedIndividually_EverythingElseIsAggregated()
    {
        // ADR-0032 §7: the shape follows the tier, and the coincidence is the
        // design — a violation has a name, hygiene and slack would flood a queue
        // with a fixed budget.
        foreach (var kind in Enum.GetValues<SignalKind>())
        {
            var expected = SignalKinds.TierOf(kind) == SignalTier.Breach
                ? SignalShape.Subject
                : SignalShape.Aggregate;

            SignalKinds.ShapeOf(kind).Should().Be(expected);
            SignalKinds.RequiresSubject(kind).Should().Be(expected == SignalShape.Subject);
        }
    }

    [Fact]
    public void KindNames_FitTheEnumColumn()
    {
        // House convention: enums persist as strings capped at 20 chars. Cheaper
        // to fail here than to discover it as a truncation at insert time.
        foreach (var kind in Enum.GetValues<SignalKind>())
            kind.ToString().Length.Should().BeLessThanOrEqualTo(20, $"'{kind}' is persisted as a string");
    }
}
