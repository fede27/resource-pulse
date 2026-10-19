using TimeSloth.Common.Results;
using TimeSloth.Domain.Allocations;
using TimeSloth.Services.Plan;

namespace TimeSloth.Application.Tests;

// Boundaries through the envelope (ADR-0035). The referent is implicit — the
// node the demand lives on — so creation takes an OPTIONAL window (a missing
// edge follows the node), followNode/pin are the two halves of one switch, and
// there is nothing to scope: the old I10 has no subject any more.
public class PlanCommandAnchorTests
{
    private static readonly DateOnly NodeStart = new(2026, 6, 1);
    private static readonly DateOnly NodeEnd = new(2026, 6, 30);
    private static readonly DateOnly Mid = new(2026, 6, 14);

    private static PlanCommandHarness Planned()
    {
        var h = PlanCommandHarness.Create();
        h.PlanNode(h.ProjectNodeId, NodeStart, NodeEnd);
        return h;
    }

    // ── Creation: the window is optional, and that is the normal case ────────

    [Fact]
    public async Task Create_WithNoDates_BornAsWideAsTheNode_FollowingOnBothEdges()
    {
        // The gesture the design cares most about: "cover this demand at 60%".
        // No drag, no dates, no referent to pick.
        var h = Planned();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            Percent = 60m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodStart.Should().Be(NodeStart);
        change.PeriodEnd.Should().Be(NodeEnd);
        change.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        change.EndAnchor.Should().Be(AnchorKind.FollowsNode);

        var stored = h.Reload(change.Id);
        stored.PeriodEnd.Should().Be(NodeEnd);
        stored.EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public async Task Create_WithOnlyAnEndDate_PinsThatEdge_AndLetsTheOtherFollow()
    {
        // "…but only until 14 Jun": exactly one supplied date, exactly one pin.
        var h = Planned();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodEnd = Mid,
            Percent = 60m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodStart.Should().Be(NodeStart);
        change.PeriodEnd.Should().Be(Mid);
        change.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        change.EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task Create_WithBothDates_PinsBothEdges_EvenWhenTheyMatchTheNode()
    {
        // A coincidence is not a decision: the server never infers a tie from
        // dates that happen to line up. The client sends what it means.
        var h = Planned();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodStart = NodeStart,
            PeriodEnd = NodeEnd,
            Percent = 60m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.StartAnchor.Should().Be(AnchorKind.Pinned);
        change.EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task Create_WithNoDatesOnAnUnplannedNode_IsRefused_NamingTheNode()
    {
        var h = PlanCommandHarness.Create(); // root left unplanned

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            Percent = 60m
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("no planned start");
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task Create_OnAPhase_FollowsThePhase_NotTheRoot()
    {
        var h = Planned();
        var phase = h.SeedPhase(h.ProjectNodeId, new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 30));
        var demand = h.SeedDemandOn(phase);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = demand,
            ResourceId = h.ResourceId,
            Percent = 60m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.ProjectNodeId.Should().Be(phase);
        change.PeriodStart.Should().Be(new DateOnly(2026, 7, 1));
        change.PeriodEnd.Should().Be(new DateOnly(2026, 8, 30));
    }

    [Fact]
    public async Task CreateByHours_SpreadsTheHoursOverTheNodeWindow()
    {
        // 8h/day fixed capacity; the node window is 1..30 Jun = 30 days = 240h.
        var h = Planned();

        var result = await h.Service.ExecuteAsync(new CreateByHoursCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            TargetHours = TimeSpan.FromHours(120)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodEnd.Should().Be(NodeEnd);
        change.AllocationPercent.Should().Be(50m);
    }

    [Fact]
    public async Task CoverInferred_CarriesTheFollowOntoTheMaterializedCoverage()
    {
        var h = Planned();
        var otherRole = h.SeedRole("Frontend"); // no demand on (node, otherRole)

        var result = await h.Service.ExecuteAsync(new CoverInferredCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = otherRole,
            ResourceId = h.ResourceId,
            Percent = 30m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.DemandChanges.Should().ContainSingle(d => d.Kind == PlanChangeKind.Created);
        var block = result.Value.Changes.Single();
        block.PeriodStart.Should().Be(NodeStart);
        block.PeriodEnd.Should().Be(NodeEnd);
        h.Reload(block.Id).EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    // ── followNode / pin: the switch ─────────────────────────────────────────

    [Fact]
    public async Task FollowNode_ReAttachesTheEdge_AndSnapsItToTheNode()
    {
        var h = Planned();
        var id = h.SeedAllocation(NodeStart, Mid); // pinned on both edges

        var result = await h.Service.ExecuteAsync(new FollowNodeCommand
        {
            Id = id,
            Edge = BoundaryEdge.End
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("followNode");
        var stored = h.Reload(id);
        stored.PeriodEnd.Should().Be(NodeEnd);
        stored.EndAnchor.Should().Be(AnchorKind.FollowsNode);
        stored.StartAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task FollowNode_OnANodeWithoutThatDate_IsRefused()
    {
        var h = PlanCommandHarness.Create();
        h.PlanNode(h.ProjectNodeId, NodeStart, null); // no planned end
        var id = h.SeedAllocation(NodeStart, Mid);

        var result = await h.Service.ExecuteAsync(new FollowNodeCommand
        {
            Id = id,
            Edge = BoundaryEdge.End
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        h.Reload(id).EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task FollowNode_ThatWouldInvertTheSpan_IsRefused_NotSilentlyResized()
    {
        var h = PlanCommandHarness.Create();
        h.PlanNode(h.ProjectNodeId, NodeStart, NodeEnd);
        var id = h.SeedAllocation(new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 20));

        var result = await h.Service.ExecuteAsync(new FollowNodeCommand
        {
            Id = id,
            Edge = BoundaryEdge.End // would pull the end back to 30 Jun, before the start
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        h.Reload(id).PeriodEnd.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public async Task Pin_ReleasesTheEdgeWithoutMovingIt()
    {
        var h = Planned();
        var id = h.SeedFollowingCoverage(h.DemandId);

        var result = await h.Service.ExecuteAsync(new PinCommand { Id = id, Edge = BoundaryEdge.End });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.PeriodEnd.Should().Be(NodeEnd);
        stored.EndAnchor.Should().Be(AnchorKind.Pinned);
        stored.StartAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public async Task FollowNode_DryRun_ShowsTheSnapAndPersistsNothing()
    {
        var h = Planned();
        var id = h.SeedAllocation(NodeStart, Mid);

        var result = await h.Service.ExecuteAsync(new FollowNodeCommand
        {
            Id = id,
            Edge = BoundaryEdge.End,
            DryRun = true
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Single().PeriodEnd.Should().Be(NodeEnd);
        h.Reload(id).PeriodEnd.Should().Be(Mid);
        h.Reload(id).EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    // ── Pin-on-move through the envelope ─────────────────────────────────────

    [Fact]
    public async Task Edit_PinsOnlyTheEdgeItMoved()
    {
        var h = Planned();
        var id = h.SeedFollowingCoverage(h.DemandId);

        var result = await h.Service.ExecuteAsync(new EditCommand
        {
            Id = id,
            PeriodStart = NodeStart,
            PeriodEnd = Mid,
            AllocationPercent = 50m
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.EndAnchor.Should().Be(AnchorKind.Pinned);
        stored.StartAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public async Task Reassign_MovesNoBoundary()
    {
        // ADR-0035 §3: the referent is the node, not the person. This is the
        // gesture that used to break a ResourceAvailability anchor.
        var h = Planned();
        var id = h.SeedFollowingCoverage(h.DemandId);

        var result = await h.Service.ExecuteAsync(new ReassignCommand
        {
            Id = id,
            ResourceId = h.OtherResourceId
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.ResourceId.Should().Be(h.OtherResourceId);
        stored.PeriodStart.Should().Be(NodeStart);
        stored.PeriodEnd.Should().Be(NodeEnd);
        stored.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        stored.EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    // ── Retarget: the referent travels with the demand ───────────────────────

    [Fact]
    public async Task Retarget_ReSnapsTheFollowingEdgesToTheNewNode()
    {
        var h = Planned();
        var id = h.SeedFollowingCoverage(h.DemandId);

        var other = h.SeedRoot();
        h.PlanNode(other, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        var target = h.SeedDemandOn(other);

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = id, DemandId = target });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.ProjectNodeId.Should().Be(other);
        stored.PeriodStart.Should().Be(new DateOnly(2026, 9, 1));
        stored.PeriodEnd.Should().Be(new DateOnly(2026, 9, 30));
        stored.StartAnchor.Should().Be(AnchorKind.FollowsNode);
        stored.EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public async Task Retarget_ToAnUnplannedNode_PinsTheEdges_KeepingTheirDates()
    {
        var h = Planned();
        var id = h.SeedFollowingCoverage(h.DemandId);

        var other = h.SeedRoot(); // deliberately unplanned
        var target = h.SeedDemandOn(other);

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = id, DemandId = target });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.PeriodStart.Should().Be(NodeStart);
        stored.PeriodEnd.Should().Be(NodeEnd);
        stored.StartAnchor.Should().Be(AnchorKind.Pinned);
        stored.EndAnchor.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task Retarget_LeavesAPinnedBlockExactlyWhereItWas()
    {
        var h = Planned();
        var id = h.SeedAllocation(NodeStart, Mid); // pinned on both edges

        var other = h.SeedRoot();
        h.PlanNode(other, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        var target = h.SeedDemandOn(other);

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = id, DemandId = target });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.PeriodStart.Should().Be(NodeStart);
        stored.PeriodEnd.Should().Be(Mid);
    }
}
