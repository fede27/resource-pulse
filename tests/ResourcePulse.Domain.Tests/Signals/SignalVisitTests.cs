using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class SignalVisitTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_RequiresAnAuthenticatedSubject()
    {
        var act = () => SignalVisit.Create("  ", Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Touch_NeverMovesTheMarkerBackwards()
    {
        // A stale request must not resurrect a batch of rows the user has already
        // read: "unseen" is FirstDetectedAt > LastVisitedAt, so moving this back
        // would silently re-flag them.
        var visit = SignalVisit.Create("sub-123", Now);

        visit.Touch(Now.AddHours(-3));

        visit.LastVisitedAt.Should().Be(Now);
    }

    [Fact]
    public void Touch_AdvancesTheMarker()
    {
        var visit = SignalVisit.Create("sub-123", Now);

        visit.Touch(Now.AddHours(3));

        visit.LastVisitedAt.Should().Be(Now.AddHours(3));
    }
}
