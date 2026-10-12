using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

// Boundary state on the atom (ADR-0035): FollowNode attaches and snaps, Pin
// releases without moving, FollowNodes moves keeping the tie, and every other
// gesture that moves an edge pins it — while the edge that did not move keeps
// what it had. There is no referent in any of these signatures: the node is
// implicit, and the service is what reads its dates.
public class AllocationAnchorTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 14);

    private static Allocation Fresh()
    {
        var a = Coverage.Cov(Resource, Node, Start, End, 50m);
        a.ClearDomainEvents();
        return a;
    }

    // A block following its node on both edges — the normal case, which under
    // ADR-0035 is also the case nothing on the board marks.
    private static Allocation Following(DateOnly? nodeStart = null, DateOnly? nodeEnd = null)
    {
        var a = Fresh();
        a.FollowNode(BoundaryEdge.Start, nodeStart ?? Start);
        a.FollowNode(BoundaryEdge.End, nodeEnd ?? End);
        a.ClearDomainEvents();
        return a;
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewCoverage_IsPinnedOnBothEdges()
    {
        // The aggregate cannot read its node, so it cannot decide to follow it.
        // The service attaches the edges after construction, which is why the
        // creation kinds and not the factory carry "no dates means follow".
        var a = Fresh();

        a.StartAnchor.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Should().Be(AnchorKind.Pinned);
        a.Follows(BoundaryEdge.Start).Should().BeFalse();
    }

    // ── FollowNode: snap + tie ───────────────────────────────────────────────

    [Fact]
    public void FollowNode_SnapsTheEdgeToTheNodeDate_AndRaisesBothEvents()
    {
        var a = Fresh();

        a.FollowNode(BoundaryEdge.End, new DateOnly(2026, 6, 30), "SetAnchor");

        a.PeriodEnd.Should().Be(new DateOnly(2026, 6, 30));
        a.PeriodStart.Should().Be(Start);
        a.EndAnchor.Should().Be(AnchorKind.FollowsNode);
        a.StartAnchor.Should().Be(AnchorKind.Pinned);

        a.DomainEvents.OfType<AllocationPeriodChanged>().Should().ContainSingle();
        var tie = a.DomainEvents.OfType<AllocationAnchorChanged>().Single();
        tie.Edge.Should().Be(BoundaryEdge.End);
        tie.OldAnchor.Should().Be(AnchorKind.Pinned);
        tie.NewAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public void FollowNode_WhenAlreadyOnTheNodeDate_RaisesOnlyTheTieEvent()
    {
        // The interesting case: "it already ends 14 Jun, but now it FOLLOWS".
        // No date moves, so no period event — the tie is the whole change, and
        // it is the one thing a drag on the board cannot express.
        var a = Fresh();

        a.FollowNode(BoundaryEdge.End, End);

        a.DomainEvents.OfType<AllocationPeriodChanged>().Should().BeEmpty();
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle();
    }

    [Fact]
    public void FollowNode_Twice_IsANoOp()
    {
        var a = Following();

        a.FollowNode(BoundaryEdge.End, End);

        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void FollowNode_ThatWouldInvertTheSpan_Throws_AndChangesNothing()
    {
        var a = Fresh();

        var act = () => a.FollowNode(BoundaryEdge.End, Start.AddDays(-1));

        act.Should().Throw<Common.Domain.DomainException>();
        a.PeriodEnd.Should().Be(End);
        a.EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    // ── Pin: release without moving ──────────────────────────────────────────

    [Fact]
    public void Pin_ReleasesTheEdge_DateUntouched()
    {
        var a = Following();

        a.Pin(BoundaryEdge.End, "Pin");

        a.PeriodEnd.Should().Be(End);
        a.EndAnchor.Should().Be(AnchorKind.Pinned);
        a.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        a.DomainEvents.OfType<AllocationPeriodChanged>().Should().BeEmpty();
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle();
    }

    [Fact]
    public void Pin_OnAPinnedEdge_IsANoOp()
    {
        var a = Fresh();

        a.Pin(BoundaryEdge.Start);

        a.DomainEvents.Should().BeEmpty();
    }

    // ── FollowNodes: propagation ─────────────────────────────────────────────

    [Fact]
    public void FollowNodes_MovesTheEdge_AndKeepsTheTie()
    {
        var a = Following();

        a.FollowNodes(null, End.AddDays(10), "ReplanNode");

        a.PeriodEnd.Should().Be(End.AddDays(10));
        a.EndAnchor.Should().Be(AnchorKind.FollowsNode);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().BeEmpty();
    }

    [Fact]
    public void FollowNodes_IsAReSnapNotADelta_TheBlockGetsLonger()
    {
        // The rule the whole model turns on: a longer phase makes the block
        // longer. It does not slide, and its duration is not preserved.
        var a = Following();
        var lengthBefore = a.PeriodEnd.DayNumber - a.PeriodStart.DayNumber;

        a.FollowNodes(null, End.AddDays(10));

        a.PeriodStart.Should().Be(Start);
        (a.PeriodEnd.DayNumber - a.PeriodStart.DayNumber).Should().Be(lengthBefore + 10);
    }

    [Fact]
    public void FollowNodes_BothEdgesAtOnce_SurvivesAShiftLongerThanTheBlock()
    {
        // Edge by edge this would invert half-way: the new start (1 Sep) is past
        // the old end (14 Jun). One span change is why it does not.
        var a = Following();

        a.FollowNodes(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14));

        a.PeriodStart.Should().Be(new DateOnly(2026, 9, 1));
        a.PeriodEnd.Should().Be(new DateOnly(2026, 9, 14));
    }

    [Fact]
    public void FollowNodes_OnAPinnedEdge_Throws()
    {
        var a = Fresh();

        var act = () => a.FollowNodes(null, End.AddDays(5));

        act.Should().Throw<Common.Domain.DomainException>()
            .WithMessage("*Pinned*");
    }

    [Fact]
    public void FollowNodes_PastTheOtherEdge_Throws_NoAutoShrink()
    {
        // The phase shrank past a pinned start: refuse, naming nothing silently
        // shortened. This is the single refusal shape left in the model.
        var a = Fresh();
        a.FollowNode(BoundaryEdge.End, End);
        a.ClearDomainEvents();

        var act = () => a.FollowNodes(null, Start.AddDays(-1));

        act.Should().Throw<Common.Domain.DomainException>();
        a.PeriodEnd.Should().Be(End);
    }

    // ── Pin-on-move: every other gesture ─────────────────────────────────────

    [Fact]
    public void ChangePeriod_PinsOnlyTheEdgeThatMoved()
    {
        var a = Following();

        a.ChangePeriod(Start, End.AddDays(3), "Edit");

        a.EndAnchor.Should().Be(AnchorKind.Pinned);
        a.StartAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public void ChangePeriod_NoOp_KeepsBothTies_AndRaisesNothing()
    {
        var a = Following();

        a.ChangePeriod(Start, End);

        a.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        a.EndAnchor.Should().Be(AnchorKind.FollowsNode);
        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Shift_PinsBothEdges()
    {
        var a = Following();

        a.Shift(7);

        a.StartAnchor.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public void Resize_PinsOnlyTheResizedEdge()
    {
        var a = Following();

        a.Resize(Start.AddDays(2), null);

        a.StartAnchor.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public void SplitAt_PinsTheCut_AndKeepsTheOuterEdges()
    {
        var a = Following();

        var second = a.SplitAt(new DateOnly(2026, 6, 8));

        a.StartAnchor.Should().Be(AnchorKind.FollowsNode);   // outer, kept
        a.EndAnchor.Should().Be(AnchorKind.Pinned);          // the cut
        second.StartAnchor.Should().Be(AnchorKind.Pinned);   // the cut
        second.EndAnchor.Should().Be(AnchorKind.FollowsNode); // outer, inherited
    }

    [Fact]
    public void SplitAt_OnPinnedBlock_RaisesNoTieEvent()
    {
        var a = Fresh();

        a.SplitAt(new DateOnly(2026, 6, 8));

        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Reassign_LeavesBothBoundariesAlone()
    {
        // ADR-0035 §3: boundaries follow the NODE, not the person. Swapping who
        // covers the work is the one gesture that moves nothing — under ADR-0034
        // it used to break a ResourceAvailability anchor.
        var a = Following();

        a.Reassign(Guid.NewGuid());

        a.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        a.EndAnchor.Should().Be(AnchorKind.FollowsNode);
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().BeEmpty();
    }

    [Fact]
    public void AnchorKind_HasExactlyTwoValues()
    {
        // The count IS the decision (ADR-0035): two states per edge, four per
        // block. A third value is a new vocabulary every reader has to learn.
        Enum.GetValues<AnchorKind>().Should().BeEquivalentTo(
            [AnchorKind.Pinned, AnchorKind.FollowsNode]);
    }
}
