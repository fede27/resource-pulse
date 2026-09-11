using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

// Boundary anchors on the atom (ADR-0034): Anchor snaps, Pin releases without
// moving, FollowReferent moves keeping the tie, and every other gesture that
// moves an edge breaks its anchor — while the edge that did not move keeps it.
public class AllocationAnchorTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid Phase = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 14);

    private static Allocation Fresh()
    {
        var a = Coverage.Cov(Resource, Node, Start, End, 50m);
        a.ClearDomainEvents();
        return a;
    }

    // A block anchored on both edges: start to the phase's start, end to its end.
    private static Allocation Anchored(DateOnly? phaseStart = null, DateOnly? phaseEnd = null)
    {
        var a = Fresh();
        a.Anchor(BoundaryEdge.Start, BoundaryAnchor.ToNodeStart(Phase), phaseStart ?? Start);
        a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(Phase), phaseEnd ?? End);
        a.ClearDomainEvents();
        return a;
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewCoverage_IsPinnedOnBothEdges()
    {
        var a = Fresh();

        a.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.StartAnchor.IsAnchored.Should().BeFalse();
    }

    // ── Anchor: snap + tie ───────────────────────────────────────────────────

    [Fact]
    public void Anchor_SnapsTheEdgeToTheReferentDate_AndRaisesBothEvents()
    {
        var a = Fresh();
        var phaseEnd = new DateOnly(2026, 6, 30);

        a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(Phase), phaseEnd, "SetAnchor");

        a.PeriodEnd.Should().Be(phaseEnd);
        a.PeriodStart.Should().Be(Start);
        a.EndAnchor.Should().Be(BoundaryAnchor.ToNodeEnd(Phase));
        a.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.DomainEvents.Should().HaveCount(2);
        a.DomainEvents.OfType<AllocationPeriodChanged>().Should().ContainSingle()
            .Which.Reason.Should().Be("SetAnchor");
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle()
            .Which.Should().Match<AllocationAnchorChanged>(e =>
                e.Edge == BoundaryEdge.End &&
                e.OldAnchor.Kind == AnchorKind.Pinned &&
                e.NewAnchor.Kind == AnchorKind.NodeEnd &&
                e.Reason == "SetAnchor");
    }

    [Fact]
    public void Anchor_WhenAlreadyOnTheReferentDate_RaisesOnlyTheAnchorEvent()
    {
        var a = Fresh();

        a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(Phase), End);

        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationAnchorChanged>();
    }

    [Fact]
    public void Anchor_SameAnchorTwice_IsANoOp()
    {
        var a = Anchored();

        a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(Phase), End);

        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Anchor_ThatWouldInvertTheSpan_Throws_AndChangesNothing()
    {
        var a = Fresh();

        var act = () => a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(Phase), Start.AddDays(-1));

        act.Should().Throw<DomainException>().WithMessage("*PeriodStart must be on or before PeriodEnd*");
        a.PeriodEnd.Should().Be(End);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public void Anchor_WithAPinnedKind_Throws()
    {
        var a = Fresh();

        var act = () => a.Anchor(BoundaryEdge.End, BoundaryAnchor.Pinned(), End);

        act.Should().Throw<DomainException>().WithMessage("*use Pin*");
    }

    // ── Pin: release without moving ──────────────────────────────────────────

    [Fact]
    public void Pin_ReleasesTheEdge_DateUntouched()
    {
        var a = Anchored();

        a.Pin(BoundaryEdge.End, "Pin");

        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.StartAnchor.Kind.Should().Be(AnchorKind.NodeStart); // the other edge keeps its tie
        a.PeriodEnd.Should().Be(End);
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationAnchorChanged>()
            .Which.Reason.Should().Be("Pin");
    }

    [Fact]
    public void Pin_OnAPinnedEdge_IsANoOp()
    {
        var a = Fresh();

        a.Pin(BoundaryEdge.Start);

        a.DomainEvents.Should().BeEmpty();
    }

    // ── FollowReferent: propagation keeps the tie ────────────────────────────

    [Fact]
    public void FollowReferent_MovesTheEdge_AndKeepsTheAnchor()
    {
        var a = Anchored();
        var newPhaseEnd = new DateOnly(2026, 7, 15);

        a.FollowReferent(BoundaryEdge.End, newPhaseEnd, "ReplanNode");

        a.PeriodEnd.Should().Be(newPhaseEnd);
        a.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationPeriodChanged>()
            .Which.Reason.Should().Be("ReplanNode");
    }

    [Fact]
    public void FollowReferent_IsAReSnapNotADelta_TheBlockGetsLonger()
    {
        var a = Anchored();

        a.FollowReferent(BoundaryEdge.End, End.AddDays(10));

        a.PeriodStart.Should().Be(Start);          // the other edge did not move
        a.PeriodEnd.Should().Be(End.AddDays(10));  // the block stretched
    }

    [Fact]
    public void FollowReferent_OnAPinnedEdge_Throws()
    {
        var a = Fresh();

        var act = () => a.FollowReferent(BoundaryEdge.End, End.AddDays(1));

        act.Should().Throw<DomainException>().WithMessage("*Pinned*");
    }

    [Fact]
    public void FollowReferent_PastTheOtherEdge_Throws_NoAutoShrink()
    {
        var a = Anchored();

        var act = () => a.FollowReferent(BoundaryEdge.End, Start.AddDays(-1));

        act.Should().Throw<DomainException>();
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
    }

    // ── Pin-on-move: gestures that move an edge break its anchor ─────────────

    [Fact]
    public void ChangePeriod_PinsOnlyTheEdgeThatMoved()
    {
        var a = Anchored();

        a.ChangePeriod(Start, End.AddDays(3));

        a.StartAnchor.Kind.Should().Be(AnchorKind.NodeStart);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle()
            .Which.Edge.Should().Be(BoundaryEdge.End);
    }

    [Fact]
    public void ChangePeriod_NoOp_KeepsBothAnchors_AndRaisesNothing()
    {
        var a = Anchored();

        a.ChangePeriod(Start, End);

        a.StartAnchor.IsAnchored.Should().BeTrue();
        a.EndAnchor.IsAnchored.Should().BeTrue();
        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Shift_PinsBothEdges()
    {
        var a = Anchored();

        a.Shift(5, "Move");

        a.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().HaveCount(2)
            .And.OnlyContain(e => e.Reason == "Move");
    }

    [Fact]
    public void Resize_PinsOnlyTheResizedEdge()
    {
        var a = Anchored();

        a.Resize(newStart: Start.AddDays(2), newEnd: null, "Resize");

        a.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
    }

    [Fact]
    public void SplitAt_PinsTheCut_AndKeepsTheOuterAnchors()
    {
        var a = Anchored();
        var cut = new DateOnly(2026, 6, 8);

        var second = a.SplitAt(cut, "Split");

        // First block: start tie kept, the cut end is pinned.
        a.StartAnchor.Kind.Should().Be(AnchorKind.NodeStart);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        // Second block: the cut start is pinned, the end tie is inherited (by value, not by instance).
        second.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        second.EndAnchor.Should().Be(BoundaryAnchor.ToNodeEnd(Phase));
        second.EndAnchor.Should().NotBeSameAs(a.EndAnchor);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle()
            .Which.Should().Match<AllocationAnchorChanged>(e => e.Edge == BoundaryEdge.End && e.Reason == "Split");
    }

    [Fact]
    public void SplitAt_OnPinnedBlock_RaisesNoAnchorEvent()
    {
        var a = Fresh();

        a.SplitAt(new DateOnly(2026, 6, 8));

        a.DomainEvents.Should().NotContain(e => e is AllocationAnchorChanged);
    }

    [Fact]
    public void Reassign_PinsOnlyResourceAvailabilityAnchors()
    {
        var a = Fresh();
        a.Anchor(BoundaryEdge.Start, BoundaryAnchor.ToNodeStart(Phase), Start);
        a.Anchor(BoundaryEdge.End, BoundaryAnchor.ToResourceAvailability(), End);
        a.ClearDomainEvents();

        a.Reassign(Guid.NewGuid());

        a.StartAnchor.Kind.Should().Be(AnchorKind.NodeStart);
        a.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        a.DomainEvents.OfType<AllocationAnchorChanged>().Should().ContainSingle()
            .Which.Reason.Should().Be("Reassign");
    }

    // ── BoundaryAnchor value object ──────────────────────────────────────────

    [Fact]
    public void BoundaryAnchor_Of_RejectsAReferentTheKindDoesNotTake()
    {
        var actNode = () => BoundaryAnchor.Of(AnchorKind.Pinned, Guid.NewGuid(), null);
        var actMissing = () => BoundaryAnchor.Of(AnchorKind.NodeEnd, null, null);
        var actConstraint = () => BoundaryAnchor.Of(AnchorKind.NodeEnd, Guid.NewGuid(), Guid.NewGuid());

        actNode.Should().Throw<DomainException>();
        actMissing.Should().Throw<DomainException>();
        actConstraint.Should().Throw<DomainException>();
    }

    [Fact]
    public void BoundaryAnchor_EqualsByValue()
    {
        var id = Guid.NewGuid();

        BoundaryAnchor.ToNodeEnd(id).Should().Be(BoundaryAnchor.ToNodeEnd(id));
        BoundaryAnchor.ToNodeEnd(id).Should().NotBe(BoundaryAnchor.ToNodeStart(id));
        BoundaryAnchor.Pinned().Should().Be(BoundaryAnchor.Pinned());
    }
}
