using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Anchoring through the envelope (ADR-0034, plan step A): anchors on the
// creation kinds snap to the referent's date (I9) and are scoped to the same
// root (I10); setAnchor / pin are gestures with a name; dryRun shows where the
// block lands without persisting; retarget across roots breaks node anchors.
public class PlanCommandAnchorTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);
    private static readonly DateOnly PhaseStart = new(2026, 6, 8);
    private static readonly DateOnly PhaseEnd = new(2026, 6, 30);

    private static AnchorSpec NodeEnd(Guid id) => new() { Kind = AnchorKind.NodeEnd, NodeId = id };
    private static AnchorSpec NodeStart(Guid id) => new() { Kind = AnchorKind.NodeStart, NodeId = id };

    // ── Creation with anchors: the referent's date wins ──────────────────────

    [Fact]
    public async Task Create_WithEndAnchor_SnapsTheEndToThePhaseEnd_AndPersistsTheTie()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodStart = D1,
            PeriodEnd = D14,           // submitted — overridden by the anchor
            Percent = 50m,
            EndAnchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodStart.Should().Be(D1);
        change.PeriodEnd.Should().Be(PhaseEnd);
        change.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
        change.EndAnchor.NodeId.Should().Be(phase);
        change.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);

        var stored = h.Reload(change.Id);
        stored.PeriodEnd.Should().Be(PhaseEnd);
        stored.EndAnchor.Should().Be(BoundaryAnchor.ToNodeEnd(phase));
    }

    [Fact]
    public async Task Create_StartAnchoredToAPhaseEnd_IsLegal_TheKindIsIndependentOfTheEdge()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, new DateOnly(2026, 6, 10));

        // "Start when phase 1 ends."
        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodStart = D1,
            PeriodEnd = PhaseEnd,
            Percent = 50m,
            StartAnchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Single().PeriodStart.Should().Be(new DateOnly(2026, 6, 10));
        result.Value.Changes.Single().StartAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
    }

    [Fact]
    public async Task CreateByHours_SpreadsTheHoursOverTheSnappedWindow()
    {
        // 8h/day fixed capacity; the snapped window is D1..PhaseEnd (30 days ⇒ 240h).
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new CreateByHoursCommand
        {
            DemandId = h.DemandId,
            ResourceId = h.ResourceId,
            PeriodStart = D1,
            PeriodEnd = D14,
            TargetHours = TimeSpan.FromHours(120),
            EndAnchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodEnd.Should().Be(PhaseEnd);
        change.AllocationPercent.Should().Be(50m); // 120h over 240h, not over the submitted 14 days
    }

    [Fact]
    public async Task CoverInferred_CarriesTheAnchorOntoTheMaterializedCoverage()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var otherRole = h.SeedRole("Frontend"); // no demand on (node, otherRole) ⇒ materialize

        var result = await h.Service.ExecuteAsync(new CoverInferredCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = otherRole,
            ResourceId = h.ResourceId,
            PeriodStart = D1,
            PeriodEnd = D14,
            Percent = 30m,
            EndAnchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.DemandChanges.Should().ContainSingle(d => d.Kind == PlanChangeKind.Created);
        var block = result.Value.Changes.Single();
        block.PeriodEnd.Should().Be(PhaseEnd);
        h.Reload(block.Id).EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
    }

    [Fact]
    public async Task Create_AnchoredToAPhaseWithoutAPlannedEnd_IsAConflict()
    {
        var h = PlanCommandHarness.Create();
        var undated = h.SeedPhase(h.ProjectNodeId, plannedStart: null, plannedEnd: null);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = NodeEnd(undated)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("no planned end");
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task Create_AnchoredToANodeOfAnotherRoot_IsAValidationError_I10()
    {
        var h = PlanCommandHarness.Create();
        var otherRoot = h.SeedRoot();
        var foreignPhase = h.SeedPhase(otherRoot, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = NodeEnd(foreignPhase)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
        result.Error.Details!.Should().ContainKey("EndAnchor");
    }

    [Fact]
    public async Task Create_AnchorsThatInvertTheSpan_IsAConflict_NothingPersisted()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        // Start anchored to the phase END (Jun 30), end anchored to the phase START (Jun 8).
        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            StartAnchor = NodeEnd(phase),
            EndAnchor = NodeStart(phase)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("invert");
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task Create_DryRun_ShowsTheSnappedWindow_AndPersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DryRun = true,
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Single().PeriodEnd.Should().Be(PhaseEnd);
        h.AllocationCount().Should().Be(0);
    }

    // ── setAnchor / pin ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetAnchor_SnapsAnExistingBlock_AndPin_ReleasesItWithoutMoving()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);

        var set = await h.Service.ExecuteAsync(new SetAnchorCommand
        {
            Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase)
        });

        set.IsSuccess.Should().BeTrue(set.Error?.Message);
        set.Value.CommandKind.Should().Be("setAnchor");
        var afterSet = h.Reload(id);
        afterSet.PeriodEnd.Should().Be(PhaseEnd);
        afterSet.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);

        var pin = await h.Service.ExecuteAsync(new PinCommand { Id = id, Edge = BoundaryEdge.End });

        pin.IsSuccess.Should().BeTrue(pin.Error?.Message);
        pin.Value.CommandKind.Should().Be("pin");
        var afterPin = h.Reload(id);
        afterPin.PeriodEnd.Should().Be(PhaseEnd);           // date untouched
        afterPin.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task SetAnchor_ThatWouldInvertTheSpan_IsAConflict_BlockUnchanged()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, new DateOnly(2026, 6, 5), 50m); // ends before the phase starts

        // Anchor the START to the phase start (Jun 8) — past the block's end (Jun 5).
        var result = await h.Service.ExecuteAsync(new SetAnchorCommand
        {
            Id = id, Edge = BoundaryEdge.Start, Anchor = NodeStart(phase)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        var stored = h.Reload(id);
        stored.PeriodStart.Should().Be(D1);
        stored.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task SetAnchor_DryRun_DoesNotPersist()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);

        var result = await h.Service.ExecuteAsync(new SetAnchorCommand
        {
            DryRun = true, Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Single().PeriodEnd.Should().Be(PhaseEnd);
        var stored = h.Reload(id);
        stored.PeriodEnd.Should().Be(D14);
        stored.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task SetAnchor_OnUnknownBlock_IsNotFound()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);

        var result = await h.Service.ExecuteAsync(new SetAnchorCommand
        {
            Id = Guid.NewGuid(), Edge = BoundaryEdge.End, Anchor = NodeEnd(phase)
        });

        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    // ── Gestures that break the tie, end to end ──────────────────────────────

    [Fact]
    public async Task Move_OnAnAnchoredBlock_PinsBothEdges()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);
        await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) });

        var result = await h.Service.ExecuteAsync(new MoveCommand { Id = id, DeltaDays = 3 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.PeriodEnd.Should().Be(PhaseEnd.AddDays(3));
        stored.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }

    [Fact]
    public async Task SplitAt_TheSecondBlockInheritsTheEndAnchor_TheFirstIsPinnedAtTheCut()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);
        await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) });

        var result = await h.Service.ExecuteAsync(new SplitAtCommand { Id = id, Date = D14 });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var first = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Modified);
        var second = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Created);
        first.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
        second.StartAnchor.Kind.Should().Be(AnchorKind.Pinned);
        second.EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
        h.Reload(second.Id).EndAnchor.NodeId.Should().Be(phase);
        h.AllocationCount().Should().Be(2);
    }

    [Fact]
    public async Task Retarget_ToADemandOnAnotherRoot_PinsTheNodeAnchor()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);
        await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) });
        var otherRoot = h.SeedRoot();
        var foreignDemand = h.SeedDemandOn(otherRoot);

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = id, DemandId = foreignDemand });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(id);
        stored.DemandId.Should().Be(foreignDemand);
        stored.PeriodEnd.Should().Be(PhaseEnd);                 // date kept
        stored.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);   // tie broken (I10)
    }

    [Fact]
    public async Task Retarget_WithinTheSameRoot_KeepsTheNodeAnchor()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        var id = h.SeedAllocation(D1, D14, 50m);
        await h.Service.ExecuteAsync(new SetAnchorCommand { Id = id, Edge = BoundaryEdge.End, Anchor = NodeEnd(phase) });
        var siblingDemand = h.SeedDemandOn(phase); // same root, on the phase itself

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = id, DemandId = siblingDemand });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        h.Reload(id).EndAnchor.Kind.Should().Be(AnchorKind.NodeEnd);
    }
}
