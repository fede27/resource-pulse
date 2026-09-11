using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Referent movement through the envelope (ADR-0034 §5, plan step B):
// replanNode / moveSubtree drag the anchored boundaries — by re-snap, never by
// delta — and dryRun returns the countable consequence without persisting.
// Pinned blocks on the same node do not move; a re-snap that would invert a
// block is a Conflict, and so is clearing a date boundaries follow.
public class PlanCommandPropagationTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);
    private static readonly DateOnly PhaseStart = new(2026, 6, 8);
    private static readonly DateOnly PhaseEnd = new(2026, 6, 30);

    private static AnchorSpec NodeEnd(Guid id) => new() { Kind = AnchorKind.NodeEnd, NodeId = id };
    private static AnchorSpec NodeStart(Guid id) => new() { Kind = AnchorKind.NodeStart, NodeId = id };

    // A block on the default demand, end-anchored to `phase`.
    private static async Task<Guid> EndAnchoredBlockAsync(PlanCommandHarness h, Guid phase, DateOnly start = default)
    {
        var id = h.SeedAllocation(start == default ? D1 : start, start == default ? D14 : PhaseEnd, 50m);
        var r = await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) });
        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        return id;
    }

    // ── replanNode ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplanNode_DryRun_ReturnsTheDraggedBlocks_AndPersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var anchored = await EndAnchoredBlockAsync(h, phase);
        var pinned = h.SeedAllocation(D1, D14, 20m); // same node, no tie
        var newEnd = new DateOnly(2026, 7, 15);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            DryRun = true, NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("replanNode");
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Should().ContainSingle().Which.Id.Should().Be(anchored);
        result.Value.Changes.Single().PeriodEnd.Should().Be(newEnd);
        result.Value.ReferentChanges.Should().ContainSingle().Which.Should().Match<PlanReferentChange>(n =>
            n.Referent == ReferentKind.Node && n.Id == phase && n.OldEnd == PhaseEnd && n.NewEnd == newEnd);

        // Nothing moved.
        h.Reload(anchored).PeriodEnd.Should().Be(PhaseEnd);
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_Commit_ReSnapsTheAnchoredBlock_KeepsTheTie_LeavesPinnedBlocksAlone()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var anchored = await EndAnchoredBlockAsync(h, phase);
        var pinned = h.SeedAllocation(D1, D14, 20m);
        var newEnd = new DateOnly(2026, 7, 15);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeTrue();
        var moved = h.Reload(anchored);
        moved.PeriodStart.Should().Be(D1);                       // the other edge stayed
        moved.PeriodEnd.Should().Be(newEnd);                     // re-snap: the block got longer
        moved.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);    // tie kept
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(newEnd);
    }

    [Fact]
    public async Task ReplanNode_ShrinkingThePhasePastTheBlockStart_IsAConflict_NothingApplied()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var anchored = await EndAnchoredBlockAsync(h, phase, start: new DateOnly(2026, 6, 20));

        // Phase end moves to Jun 10 — before the block's start (Jun 20).
        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = new DateOnly(2026, 6, 10)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain(anchored.ToString());
        h.Reload(anchored).PeriodEnd.Should().Be(PhaseEnd);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_ClearingADateBoundariesFollow_IsAConflictWithTheCount()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        await EndAnchoredBlockAsync(h, phase);
        await EndAnchoredBlockAsync(h, phase);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = null
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("2 anchored boundary(ies)");
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_WithNoDependants_JustReplansTheNode()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = PhaseEnd.AddDays(5)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().BeEmpty();
        result.Value.ReferentChanges.Should().ContainSingle();
    }

    [Fact]
    public async Task ReplanNode_OnUnknownNode_IsNotFound()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand { NodeId = Guid.NewGuid(), PlannedEnd = PhaseEnd });

        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task ReplanNode_BlockAnchoredOnBothEdges_FollowsAShiftLongerThanItself()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.Start, Anchor = NodeStart(phase) }))
            .IsSuccess.Should().BeTrue();
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) }))
            .IsSuccess.Should().BeTrue();
        // Block is now [Jun 8, Jun 30]. Shift the phase by 60 days: edge by edge
        // the new start (Aug 7) would be past the old end (Jun 30).
        var newStart = PhaseStart.AddDays(60);
        var newEnd = PhaseEnd.AddDays(60);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = newStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var moved = h.Reload(id);
        moved.PeriodStart.Should().Be(newStart);
        moved.PeriodEnd.Should().Be(newEnd);
        moved.StartAnchor.IsAnchored.Should().BeTrue();
        moved.EndAnchor.IsAnchored.Should().BeTrue();
    }

    // ── moveSubtree ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveSubtree_TranslatesRootAndPhases_AndDragsBlocksAnchoredToAnyOfThem()
    {
        var h = PlanCommandHarness.Create();
        var rootStart = new DateOnly(2026, 5, 1);
        var rootEnd = new DateOnly(2026, 8, 31);
        h.Db.ProjectNodes.Single(p => p.Id == h.ProjectNodeId).Replan(rootStart, rootEnd);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var undatedPhase = h.SeedPhase(h.ProjectNodeId, null, null, "Senza date");

        var onPhase = await EndAnchoredBlockAsync(h, phase);
        var onRoot = h.SeedAllocation(D1, D14, 30m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = onRoot, Edge = BoundaryEdge.End, Anchor = NodeEnd(h.ProjectNodeId) }))
            .IsSuccess.Should().BeTrue();
        var pinned = h.SeedAllocation(D1, D14, 10m);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { NodeId = h.ProjectNodeId, DeltaDays = 7 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("moveSubtree");
        // Two dated nodes moved; the undated phase has nothing to translate.
        result.Value.ReferentChanges.Should().HaveCount(2);
        result.Value.ReferentChanges.Should().NotContain(n => n.Id == undatedPhase);
        result.Value.Changes.Select(c => c.Id).Should().BeEquivalentTo([onPhase, onRoot]);

        h.Reload(onPhase).PeriodEnd.Should().Be(PhaseEnd.AddDays(7));
        h.Reload(onRoot).PeriodEnd.Should().Be(rootEnd.AddDays(7));
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        var nodes = h.Db.ProjectNodes.AsNoTracking().ToDictionary(p => p.Id);
        nodes[h.ProjectNodeId].PlannedStart.Should().Be(rootStart.AddDays(7));
        nodes[phase].PlannedEnd.Should().Be(PhaseEnd.AddDays(7));
        nodes[undatedPhase].PlannedEnd.Should().BeNull();
    }

    [Fact]
    public async Task MoveSubtree_OnAPhase_DoesNotTouchTheRootOrSiblings()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var sibling = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd, "Fase 2");
        await EndAnchoredBlockAsync(h, phase);
        var onSibling = await EndAnchoredBlockAsync(h, sibling);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { NodeId = phase, DeltaDays = -3 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ReferentChanges.Should().ContainSingle().Which.Id.Should().Be(phase);
        result.Value.Changes.Should().ContainSingle();
        h.Reload(onSibling).PeriodEnd.Should().Be(PhaseEnd);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == sibling).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task MoveSubtree_DryRun_PersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var anchored = await EndAnchoredBlockAsync(h, phase);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { DryRun = true, NodeId = phase, DeltaDays = 10 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().ContainSingle().Which.PeriodEnd.Should().Be(PhaseEnd.AddDays(10));
        h.Reload(anchored).PeriodEnd.Should().Be(PhaseEnd);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task MoveSubtree_OnAClosedProject_IsAConflict_I4()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var root = h.Db.ProjectNodes.Single(p => p.Id == h.ProjectNodeId);
        root.Start();
        root.Complete();
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { NodeId = phase, DeltaDays = 1 });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }
}
