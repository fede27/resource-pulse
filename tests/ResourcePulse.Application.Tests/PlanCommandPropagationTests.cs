using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Moving the node through the envelope (ADR-0035 §5). The node is now the ONLY
// referent, so it is the only thing that can drag a boundary, and the
// dependants are simply the coverages sitting on it. Propagation is a re-snap,
// never a delta; dryRun returns the countable consequence without persisting;
// pinned edges on the same node do not move; a re-snap that would invert a
// block is a Conflict, and so is clearing a date boundaries follow.
public class PlanCommandPropagationTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);
    private static readonly DateOnly PhaseStart = new(2026, 6, 8);
    private static readonly DateOnly PhaseEnd = new(2026, 6, 30);

    // A coverage ON `node`, its end FOLLOWING the node, its start pinned at
    // `start`. The demand is seeded on the node because that is what makes the
    // node this block's referent — there is no other way to point at it.
    private static async Task<Guid> EndFollowingBlockAsync(
        PlanCommandHarness h, Guid node, DateOnly start, DateOnly end, decimal percent = 50m)
    {
        var demand = h.SeedDemandOn(node);
        var id = h.SeedCoverage(demand, start, end, percent);
        var r = await h.Service.ExecuteAsync(new FollowNodeCommand { Id = id, Edge = BoundaryEdge.End });
        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        return id;
    }

    // A coverage ON `node` with both edges pinned — same node, no tie.
    private static Guid PinnedBlockOn(PlanCommandHarness h, Guid node, DateOnly start, DateOnly end, decimal percent = 20m) =>
        h.SeedCoverage(h.SeedDemandOn(node), start, end, percent);

    // ── replanNode ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplanNode_DryRun_ReturnsTheDraggedBlocks_AndPersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var following = await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);
        var pinned = PinnedBlockOn(h, phase, D1, D14);
        var newEnd = new DateOnly(2026, 7, 15);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            DryRun = true, NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("replanNode");
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Should().ContainSingle().Which.Id.Should().Be(following);
        result.Value.Changes.Single().PeriodEnd.Should().Be(newEnd);
        result.Value.ReferentChanges.Should().ContainSingle().Which.Should().Match<PlanReferentChange>(n =>
            n.Id == phase && n.OldEnd == PhaseEnd && n.NewEnd == newEnd);

        // Nothing moved.
        h.Reload(following).PeriodEnd.Should().Be(PhaseEnd);
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_Commit_ReSnaps_KeepsTheTie_LeavesPinnedBlocksOnTheSameNodeAlone()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var following = await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);
        var pinned = PinnedBlockOn(h, phase, D1, D14);
        var newEnd = new DateOnly(2026, 7, 15);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeTrue();
        var moved = h.Reload(following);
        moved.PeriodStart.Should().Be(D1);                        // the other edge stayed
        moved.PeriodEnd.Should().Be(newEnd);                      // re-snap: the block got longer
        moved.EndAnchor.Should().Be(AnchorKind.FollowsNode);      // tie kept
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(newEnd);
    }

    [Fact]
    public async Task ReplanNode_DoesNotTouchBlocksOnOtherNodes()
    {
        // No propagation between sibling phases: moving Fase 2 does not move
        // Fase 3, and the overlap that results is legal and sums (ADR-0014).
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd, "Fase 2");
        var sibling = h.SeedPhase(h.ProjectNodeId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "Fase 3");
        await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);
        var onSibling = await EndFollowingBlockAsync(h, sibling, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = new DateOnly(2026, 7, 15)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().ContainSingle();
        h.Reload(onSibling).PeriodEnd.Should().Be(new DateOnly(2026, 7, 31));
    }

    [Fact]
    public async Task ReplanNode_ShrinkingThePhasePastAPinnedStart_IsAConflict_NothingApplied()
    {
        // The single refusal shape left in the model, and it names the block.
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var following = await EndFollowingBlockAsync(h, phase, D14, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = new DateOnly(2026, 6, 9)
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain(following.ToString());
        h.Reload(following).PeriodEnd.Should().Be(PhaseEnd);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_ClearingADateBoundariesFollow_IsAConflictWithTheCount()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = null
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage boundary(ies) follow");
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task ReplanNode_WithNoDependants_JustReplansTheNode()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var newEnd = new DateOnly(2026, 7, 15);

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().BeEmpty();
        result.Value.ReferentChanges.Should().ContainSingle();
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(newEnd);
    }

    [Fact]
    public async Task ReplanNode_OnUnknownNode_IsNotFound()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = Guid.NewGuid(), PlannedStart = PhaseStart, PlannedEnd = PhaseEnd
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task ReplanNode_BlockFollowingOnBothEdges_FollowsAShiftLongerThanItself()
    {
        // Edge by edge this would invert half-way. One span change is why it
        // does not — and why FollowNodes takes both edges at once.
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var demand = h.SeedDemandOn(phase);
        var id = h.SeedFollowingCoverage(demand);

        var newStart = new DateOnly(2026, 9, 1);
        var newEnd = new DateOnly(2026, 9, 24);
        var result = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = newStart, PlannedEnd = newEnd
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var moved = h.Reload(id);
        moved.PeriodStart.Should().Be(newStart);
        moved.PeriodEnd.Should().Be(newEnd);
    }

    // ── moveSubtree ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveSubtree_TranslatesRootAndPhases_AndDragsWhatFollowsAnyOfThem()
    {
        var h = PlanCommandHarness.Create();
        h.PlanNode(h.ProjectNodeId, D1, new DateOnly(2026, 8, 31));
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var onRoot = h.SeedFollowingCoverage(h.DemandId);
        var onPhase = await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand
        {
            NodeId = h.ProjectNodeId, DeltaDays = 10
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ReferentChanges.Should().HaveCount(2);
        result.Value.Changes.Select(c => c.Id).Should().BeEquivalentTo([onRoot, onPhase]);
        h.Reload(onRoot).PeriodEnd.Should().Be(new DateOnly(2026, 9, 10));
        h.Reload(onPhase).PeriodEnd.Should().Be(PhaseEnd.AddDays(10));
        h.Reload(onPhase).PeriodStart.Should().Be(D1); // pinned edge stays
    }

    [Fact]
    public async Task MoveSubtree_OnAPhase_DoesNotTouchTheRootOrSiblings()
    {
        var h = PlanCommandHarness.Create();
        h.PlanNode(h.ProjectNodeId, D1, new DateOnly(2026, 8, 31));
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd, "Fase 2");
        var sibling = h.SeedPhase(h.ProjectNodeId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "Fase 3");
        var onRoot = h.SeedFollowingCoverage(h.DemandId);
        var onSibling = await EndFollowingBlockAsync(h, sibling, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var onPhase = await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { NodeId = phase, DeltaDays = 10 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().ContainSingle().Which.Id.Should().Be(onPhase);
        h.Reload(onRoot).PeriodEnd.Should().Be(new DateOnly(2026, 8, 31));
        h.Reload(onSibling).PeriodEnd.Should().Be(new DateOnly(2026, 7, 31));
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == h.ProjectNodeId).PlannedEnd
            .Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public async Task MoveSubtree_DryRun_PersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var onPhase = await EndFollowingBlockAsync(h, phase, D1, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand
        {
            DryRun = true, NodeId = phase, DeltaDays = 10
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Should().ContainSingle();
        h.Reload(onPhase).PeriodEnd.Should().Be(PhaseEnd);
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

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand { NodeId = phase, DeltaDays = 10 });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }
}
