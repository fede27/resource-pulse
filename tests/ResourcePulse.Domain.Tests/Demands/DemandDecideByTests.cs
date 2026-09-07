using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Demands;

// The EXPLICIT decision deadline (ADR-0033 §3). An override, not the normal case:
// null means "derive it" — node.PlannedStart − the org lead time — which is what
// gives every demand a deadline at zero friction.
public class DemandDecideByTests
{
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid Role = Guid.NewGuid();

    private static Demand Seed(DateOnly? decideBy = null)
    {
        var d = Demand.Create(Node, Role, TimeSpan.FromHours(40), DemandProvenance.Declared, decideBy: decideBy);
        d.ClearDomainEvents();
        return d;
    }

    [Fact]
    public void ADemandIsCreatedWithoutAnOverride_ByDefault() =>
        Seed().DecideBy.Should().BeNull();

    [Fact]
    public void TheOverrideCanBeSetAtCreation() =>
        Seed(new DateOnly(2026, 7, 1)).DecideBy.Should().Be(new DateOnly(2026, 7, 1));

    [Fact]
    public void ChangeDecideBy_SetsTheOverride_AndRaisesEvent()
    {
        var d = Seed();

        d.ChangeDecideBy(new DateOnly(2026, 7, 1));

        d.DecideBy.Should().Be(new DateOnly(2026, 7, 1));
        d.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DemandDecideByChanged>()
            .Which.Should().Match<DemandDecideByChanged>(e =>
                e.OldDecideBy == null && e.NewDecideBy == new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void ClearingTheOverride_ReturnsToTheDerivedDeadline_NotToNoUrgency()
    {
        var d = Seed(new DateOnly(2026, 7, 1));

        d.ChangeDecideBy(null);

        d.DecideBy.Should().BeNull();
        d.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DemandDecideByChanged>();
    }

    [Fact]
    public void NoOpChange_SuppressesTheEvent()
    {
        // House convention since Phase 3 (Replan, AssignToTeam): a mutation that
        // changes nothing raises nothing.
        var d = Seed(new DateOnly(2026, 7, 1));

        d.ChangeDecideBy(new DateOnly(2026, 7, 1));

        d.DomainEvents.Should().BeEmpty();
    }
}
