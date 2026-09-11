using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// The third anchor type (ADR-0034 §4, plan step D): a boundary tied to an
// imposed date on the root project. Scoped to the same root (I10); an imposed
// date always exists, so I9 has nothing to refuse. moveConstraint is the
// referent mover — the renegotiation gesture.
public class PlanCommandConstraintTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);
    private static readonly DateOnly GoLive = new(2026, 9, 15);

    private static AnchorSpec External(Guid id) => new() { Kind = AnchorKind.External, ConstraintId = id };

    [Fact]
    public async Task Create_WithEndAnchoredToAConstraint_SnapsToItsDate()
    {
        var h = PlanCommandHarness.Create();
        var goLive = h.SeedConstraint(h.ProjectNodeId, GoLive);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = External(goLive)
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var change = result.Value.Changes.Single();
        change.PeriodEnd.Should().Be(GoLive);
        change.EndAnchor.Kind.Should().Be(AnchorKind.External);
        change.EndAnchor.ConstraintId.Should().Be(goLive);
        h.Reload(change.Id).EndAnchor.Should().Be(BoundaryAnchor.ToExternal(goLive));
    }

    [Fact]
    public async Task Create_AnchoredToAConstraintOfAnotherRoot_IsAValidationError_I10()
    {
        var h = PlanCommandHarness.Create();
        var otherRoot = h.SeedRoot();
        var foreign = h.SeedConstraint(otherRoot, GoLive);

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = External(foreign)
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
        result.Error.Details!.Should().ContainKey("EndAnchor");
    }

    [Fact]
    public async Task Create_AnchoredToAnUnknownConstraint_IsAValidationError()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 50m,
            EndAnchor = External(Guid.NewGuid())
        });

        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
    }

    [Fact]
    public async Task MoveConstraint_DragsTheAnchoredEnd_AndReportsTheReferent()
    {
        var h = PlanCommandHarness.Create();
        var goLive = h.SeedConstraint(h.ProjectNodeId, GoLive);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = External(goLive) }))
            .IsSuccess.Should().BeTrue();
        var pinned = h.SeedAllocation(D1, D14, 20m);
        var renegotiated = GoLive.AddDays(45);

        var result = await h.Service.ExecuteAsync(new MoveConstraintCommand { ConstraintId = goLive, Date = renegotiated });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.CommandKind.Should().Be("moveConstraint");
        result.Value.Changes.Should().ContainSingle().Which.Id.Should().Be(anchored);
        result.Value.ReferentChanges.Should().ContainSingle().Which.Should().Match<PlanReferentChange>(r =>
            r.Referent == ReferentKind.ExternalConstraint && r.Id == goLive && r.OldEnd == GoLive && r.NewEnd == renegotiated);

        var moved = h.Reload(anchored);
        moved.PeriodEnd.Should().Be(renegotiated);
        moved.EndAnchor.Kind.Should().Be(AnchorKind.External);
        h.Reload(pinned).PeriodEnd.Should().Be(D14);
        h.Db.ExternalConstraints.AsNoTracking().Single(c => c.Id == goLive).Date.Should().Be(renegotiated);
    }

    [Fact]
    public async Task MoveConstraint_DryRun_PersistsNothing()
    {
        var h = PlanCommandHarness.Create();
        var goLive = h.SeedConstraint(h.ProjectNodeId, GoLive);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = External(goLive) }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new MoveConstraintCommand { DryRun = true, ConstraintId = goLive, Date = GoLive.AddDays(10) });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Single().PeriodEnd.Should().Be(GoLive.AddDays(10));
        h.Reload(anchored).PeriodEnd.Should().Be(GoLive);
        h.Db.ExternalConstraints.AsNoTracking().Single(c => c.Id == goLive).Date.Should().Be(GoLive);
    }

    [Fact]
    public async Task MoveConstraint_BeforeTheBlockStart_IsAConflict_NothingApplied()
    {
        var h = PlanCommandHarness.Create();
        var goLive = h.SeedConstraint(h.ProjectNodeId, GoLive);
        var anchored = h.SeedAllocation(new DateOnly(2026, 8, 1), D14.AddDays(60), 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = External(goLive) }))
            .IsSuccess.Should().BeTrue();

        var result = await h.Service.ExecuteAsync(new MoveConstraintCommand { ConstraintId = goLive, Date = new DateOnly(2026, 7, 1) });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        h.Reload(anchored).PeriodEnd.Should().Be(GoLive);
        h.Db.ExternalConstraints.AsNoTracking().Single(c => c.Id == goLive).Date.Should().Be(GoLive);
    }

    [Fact]
    public async Task MoveConstraint_OnUnknownConstraint_IsNotFound()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new MoveConstraintCommand { ConstraintId = Guid.NewGuid(), Date = GoLive });

        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task Retarget_ToADemandOnAnotherRoot_PinsTheExternalAnchor()
    {
        var h = PlanCommandHarness.Create();
        var goLive = h.SeedConstraint(h.ProjectNodeId, GoLive);
        var anchored = h.SeedAllocation(D1, D14, 50m);
        (await h.Service.ExecuteAsync(new SetAnchorCommand { Id = anchored, Edge = BoundaryEdge.End, Anchor = External(goLive) }))
            .IsSuccess.Should().BeTrue();
        var otherRoot = h.SeedRoot();
        var foreignDemand = h.SeedDemandOn(otherRoot);

        var result = await h.Service.ExecuteAsync(new RetargetCommand { Id = anchored, DemandId = foreignDemand });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = h.Reload(anchored);
        stored.PeriodEnd.Should().Be(GoLive);
        stored.EndAnchor.Kind.Should().Be(AnchorKind.Pinned);
    }
}
