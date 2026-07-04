using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;

namespace ResourcePulse.Domain.Tests.Capacity;

// Demand-vs-coverage reconciliation (Phase 5.2, ADR-0025/0026): covered = % ×
// capacity; gap = required − covered; best-effort ⇒ null gap; over-coverage ⇒
// negative gap; zero capacity ⇒ 0 covered.
public class LoadCalculator_DemandCoverageTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid Role = Guid.NewGuid();
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5); // 5 days

    private static Demand DemandWith(TimeSpan? required) =>
        Demand.Create(Node, Role, required, DemandProvenance.Declared);

    // 8h/day capacity for R1 over [Mon, Fri].
    private static Dictionary<(Guid, DateOnly), TimeSpan> Cap8h(Guid resource = default)
    {
        var rid = resource == default ? R1 : resource;
        var d = new Dictionary<(Guid, DateOnly), TimeSpan>();
        for (var date = Mon; date <= Fri; date = date.AddDays(1))
            d[(rid, date)] = TimeSpan.FromHours(8);
        return d;
    }

    [Fact]
    public void CoveredHours_IsPercentTimesCapacity_OverRange()
    {
        var demand = DemandWith(TimeSpan.FromHours(200));
        var cov = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 50m); // 50% of 8h × 5d = 20h

        var result = LoadCalculator.CoverageForDemands([demand], [cov], Cap8h(), Mon, Fri);

        var c = result.Should().ContainSingle().Subject;
        c.CoveredHours.Should().Be(TimeSpan.FromHours(20));
        c.RequiredHours.Should().Be(TimeSpan.FromHours(200));
        c.GapHours.Should().Be(TimeSpan.FromHours(180)); // 200 − 20
    }

    [Fact]
    public void BestEffort_NullRequired_YieldsNullGap_ButCoveredComputed()
    {
        var demand = DemandWith(null);
        var cov = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 50m);

        var c = LoadCalculator.CoverageForDemands([demand], [cov], Cap8h(), Mon, Fri).Single();

        c.RequiredHours.Should().BeNull();
        c.GapHours.Should().BeNull();
        c.CoveredHours.Should().Be(TimeSpan.FromHours(20)); // consumption-without-reference
    }

    [Fact]
    public void OverCoverage_YieldsNegativeGap_Surplus()
    {
        var demand = DemandWith(TimeSpan.FromHours(10));
        var cov = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 50m); // 20h covered > 10h required

        var c = LoadCalculator.CoverageForDemands([demand], [cov], Cap8h(), Mon, Fri).Single();

        c.CoveredHours.Should().Be(TimeSpan.FromHours(20));
        c.GapHours.Should().Be(TimeSpan.FromHours(-10)); // surplus, not clamped
    }

    [Fact]
    public void NoCoverage_CoveredZero_GapEqualsRequired()
    {
        var demand = DemandWith(TimeSpan.FromHours(40));

        var c = LoadCalculator.CoverageForDemands([demand], [], Cap8h(), Mon, Fri).Single();

        c.CoveredHours.Should().Be(TimeSpan.Zero);
        c.GapHours.Should().Be(TimeSpan.FromHours(40));
    }

    [Fact]
    public void MultipleCoverage_OnSameDemand_Sum()
    {
        var demand = DemandWith(TimeSpan.FromHours(100));
        var a = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 50m); // 20h
        var b = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 25m); // 10h

        var c = LoadCalculator.CoverageForDemands([demand], [a, b], Cap8h(), Mon, Fri).Single();

        c.CoveredHours.Should().Be(TimeSpan.FromHours(30));
    }

    [Fact]
    public void ZeroCapacityDay_ContributesNoCoveredHours()
    {
        var demand = DemandWith(TimeSpan.FromHours(40));
        var cov = Coverage.CovOn(demand.Id, R1, Node, Mon, Fri, 100m);
        var cap = new Dictionary<(Guid, DateOnly), TimeSpan>(); // empty ⇒ zero capacity everywhere

        var c = LoadCalculator.CoverageForDemands([demand], [cov], cap, Mon, Fri).Single();

        c.CoveredHours.Should().Be(TimeSpan.Zero);
        c.GapHours.Should().Be(TimeSpan.FromHours(40)); // nothing covered
    }
}
