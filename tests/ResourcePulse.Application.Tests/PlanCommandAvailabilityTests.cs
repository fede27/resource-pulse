using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// The second anchor type (ADR-0034 §3, plan step C): a boundary tied to the
// person's declared availability. The referent is implicit — the block's own
// resource — so the anchor carries no id; a start edge follows AvailableFrom,
// an end edge follows AvailableUntil. setAvailability is the referent mover.
public class PlanCommandAvailabilityTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);
    private static readonly DateOnly Until = new(2026, 7, 31);
    private static readonly AnchorSpec Availability = new() { Kind = AnchorKind.ResourceAvailability };

    [Fact]
    public async Task Create_WithEndAnchoredToAvailability_SnapsToAvailableUntil()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = Availability
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodEnd.Should().Be(Until);
        change.EndAnchor.Kind.Should().Be(AnchorKind.ResourceAvailability);
        change.EndAnchor.NodeId.Should().BeNull();
        h.Reload(change.Id).EndAnchor.Should().Be(BoundaryAnchor.ToResourceAvailability());
    }

    [Fact]
    public async Task Create_AnchoredToAnUndeclaredBoundary_IsAConflict()
    {
        var h = PlanCommandHarness.Create(); // no availability declared

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = Availability
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("AvailableUntil");
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task SetAvailability_DragsTheAnchoredEnd_LeavesNodeAnchorsAndPinnedBlocksAlone()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = Availability }))
            .IsSuccess.Should().BeTrue();
        var pinned = h.SeedAllocation(D1, D14, 20m);
        var newUntil = new DateOnly(2026, 9, 30);

        var result = await h.Service.ExecuteAsync(new SetAvailabilityCommand
        {
            ResourceId = h.ResourceId, AvailableFrom = null, AvailableUntil = newUntil
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("setAvailability");
        result.Value.Changes.Should().ContainSingle().Which.Id.Should().Be(anchored);
        result.Value.ReferentChanges.Should().ContainSingle().Which.Should().Match<PlanReferentChange>(r =>
            r.Referent == ReferentKind.Resource && r.Id == h.ResourceId && r.OldEnd == Until && r.NewEnd == newUntil);

        var moved = h.Reload(anchored);
        moved.PeriodEnd.Should().Be(newUntil);
        moved.EndAnchor.Kind.Should().Be(AnchorKind.ResourceAvailability);
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.Resources.AsNoTracking().Single(r => r.Id == h.ResourceId).AvailableUntil.Should().Be(newUntil);
    }

    [Fact]
    public async Task SetAvailability_DryRun_PersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = Availability }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new SetAvailabilityCommand
        {
            DryRun = true, ResourceId = h.ResourceId, AvailableUntil = Until.AddDays(30)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Single().PeriodEnd.Should().Be(Until.AddDays(30));
        h.Reload(anchored).PeriodEnd.Should().Be(Until);
        h.Db.Resources.AsNoTracking().Single(r => r.Id == h.ResourceId).AvailableUntil.Should().Be(Until);
    }

    [Fact]
    public async Task SetAvailability_ClearingAFollowedBoundary_IsAConflictWithTheCount()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = Availability }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new SetAvailabilityCommand
        {
            ResourceId = h.ResourceId, AvailableFrom = null, AvailableUntil = null
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 anchored boundary(ies)");
        h.Db.Resources.AsNoTracking().Single(r => r.Id == h.ResourceId).AvailableUntil.Should().Be(Until);
    }

    [Fact]
    public async Task SetAvailability_MovingUntilBeforeTheBlockStart_IsAConflict_NothingApplied()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);
        var anchored = h.SeedAllocation(new DateOnly(2026, 7, 1), D14.AddDays(30), 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = Availability }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new SetAvailabilityCommand
        {
            ResourceId = h.ResourceId, AvailableUntil = new DateOnly(2026, 6, 15)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        h.Reload(anchored).PeriodEnd.Should().Be(Until);
        h.Db.Resources.AsNoTracking().Single(r => r.Id == h.ResourceId).AvailableUntil.Should().Be(Until);
    }

    [Fact]
    public async Task Reassign_ThroughTheEnvelope_PinsTheAvailabilityAnchor()
    {
        var h = PlanCommandHarness.Create();
        h.SetResourceAvailability(h.ResourceId, from: null, until: Until);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = Availability }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new ReassignCommand { Id = anchored, ResourceId = h.OtherResourceId });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(anchored);
        stored.ResourceId.Should().Be(h.OtherResourceId);
        stored.PeriodEnd.Should().Be(Until);                        // date kept
        stored.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);       // tie broken: the referent changed
    }

    [Fact]
    public async Task SetAvailability_OnUnknownResource_IsNotFound()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new SetAvailabilityCommand { ResourceId = Guid.NewGuid(), AvailableUntil = Until });

        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }
}
