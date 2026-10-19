using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Results;
using TimeSloth.Domain.Allocations;
using TimeSloth.Domain.Projects;
using TimeSloth.Domain.Roles;
using TimeSloth.Domain.Teams;
using TimeSloth.Persistence;
using TimeSloth.Services.Plan;
using TimeSloth.Services.Projects;
using TimeSloth.Services.Roles;
using TimeSloth.Services.Teams;

namespace TimeSloth.Application.Tests;

// Every refusal coded under ADR-0037 is asserted here by its CODE and its
// PIECES — what the client branches on and translates — never by its English
// `detail`, which stays free to change. One test per site.
public class ProblemCodeSitesTests
{
    private static readonly DateOnly Jun1 = new(2026, 6, 1);
    private static readonly DateOnly Jun10 = new(2026, 6, 10);
    private static readonly DateOnly Jun20 = new(2026, 6, 20);
    private static readonly DateOnly Jun30 = new(2026, 6, 30);

    private static ServiceError Refused<T>(ServiceResult<T> result, ProblemCode code)
    {
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(code);
        result.Error.Kind.Should().Be(ServiceErrorKind.Conflict);
        return result.Error;
    }

    private static ProjectNodeService Projects(PlanCommandHarness h)
    {
        var mapping = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(mapping);
        return new ProjectNodeService(
            new Repository<ProjectNode, Guid>(h.Db), h.Db, new Mapper(mapping), h.CommitmentPolicy);
    }

    // ── The plan ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task I4_AClosedRoot_NamesTheProjectAndItsStatus()
    {
        var h = PlanCommandHarness.Create();
        var root = h.Db.ProjectNodes.Single(p => p.Id == h.ProjectNodeId);
        root.Cancel("no budget");
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        var error = Refused(await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId, PeriodStart = Jun1, PeriodEnd = Jun30, Percent = 50m,
        }), ProblemCode.ProjectRootClosed);

        error.Args!["project"].Should().Be("Proj");
        error.Args["status"].Should().Be(ProjectStatus.Cancelled);
    }

    [Fact]
    public async Task I3_AnInactivePerson_IsNamed()
    {
        var h = PlanCommandHarness.Create();
        h.Db.Resources.Single(r => r.Id == h.ResourceId).Deactivate();
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        var error = Refused(await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId, PeriodStart = Jun1, PeriodEnd = Jun30, Percent = 50m,
        }), ProblemCode.ResourceInactive);

        error.Args!["resource"].Should().Be("Tizio");
    }

    [Fact]
    public async Task FollowingAnUnplannedNode_NamesTheNodeAndTheEdge()
    {
        // The default root is seeded unplanned: a dateless coverage has nothing to follow.
        var h = PlanCommandHarness.Create();

        var error = Refused(await h.Service.ExecuteAsync(new CreateCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId, Percent = 50m,
        }), ProblemCode.NodeHasNoPlannedDate);

        error.Args!["node"].Should().Be("Proj");
        error.Args["edge"].Should().Be(BoundaryEdge.Start);
    }

    [Fact]
    public async Task ClearingAFollowedDate_NamesTheNodeTheEdgeAndTheCount()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, Jun1, Jun30, "Analisi");
        var demand = h.SeedDemandOn(phase);
        h.SeedFollowingCoverage(demand);
        h.SeedFollowingCoverage(demand, resourceId: h.OtherResourceId);

        var error = Refused(await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = Jun1, PlannedEnd = null,
        }), ProblemCode.PlannedDateFollowed);

        error.Args!["node"].Should().Be("Analisi");
        error.Args["edge"].Should().Be(BoundaryEdge.End);
        error.Args["count"].Should().Be(2);
    }

    [Fact]
    public async Task AResnapThatWouldInvertABlock_NamesTheNodeThePersonAndTheBlock()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, Jun1, Jun30, "Analisi");
        var demand = h.SeedDemandOn(phase);
        var block = h.SeedFollowingCoverage(demand);
        // Resizing pins the moved edge: end pinned on 10 Jun, start still following.
        (await h.Service.ExecuteAsync(new ResizeCommand { Id = block, NewPeriodEnd = Jun10 })).IsSuccess.Should().BeTrue();

        // The phase now starts after the block's pinned end.
        var error = Refused(await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            NodeId = phase, PlannedStart = Jun20, PlannedEnd = Jun30,
        }), ProblemCode.FollowWouldInvertCoverage);

        error.Args!["node"].Should().Be("Analisi");
        error.Args["resource"].Should().Be("Tizio");
        error.Args["from"].Should().Be(Jun1);
        error.Args["to"].Should().Be(Jun10);
    }

    [Fact]
    public async Task HoursOnAPersonWithNoCapacity_NameThePersonAndTheWindow()
    {
        var h = PlanCommandHarness.Create(capacityPerDay: TimeSpan.Zero);

        var error = Refused(await h.Service.ExecuteAsync(new CreateByHoursCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId, PeriodStart = Jun1, PeriodEnd = Jun30,
            TargetHours = TimeSpan.FromHours(40),
        }), ProblemCode.ZeroCapacityInWindow);

        error.Args!["resource"].Should().Be("Tizio");
        error.Args["from"].Should().Be(Jun1);
        error.Args["to"].Should().Be(Jun30);
    }

    [Fact]
    public async Task HoursAboveTheCeiling_CarryTheResolvedPercentAndTheCap()
    {
        var h = PlanCommandHarness.Create(capacityPerDay: TimeSpan.FromHours(1));

        var error = Refused(await h.Service.ExecuteAsync(new CreateByHoursCommand
        {
            DemandId = h.DemandId, ResourceId = h.ResourceId, PeriodStart = Jun1, PeriodEnd = Jun1,
            TargetHours = TimeSpan.FromHours(20),
        }), ProblemCode.HoursAbovePercentCap);

        error.Args!["percent"].Should().Be(2000m);
        error.Args["cap"].Should().Be(Allocation.MaxAllocationPercent);
    }

    [Fact]
    public async Task DeletingACoveredDemand_CarriesTheCount()
    {
        var h = PlanCommandHarness.Create();
        h.SeedAllocation(Jun1, Jun10);
        h.SeedAllocation(Jun20, Jun30);

        var error = Refused(await h.Service.ExecuteAsync(new DeleteDemandCommand { Id = h.DemandId }),
            ProblemCode.DemandHasCoverage);

        error.Args!["count"].Should().Be(2);
    }

    // ── The anagrafica ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReplanningAFollowedNodeFromTheAnagrafica_CarriesTheCountAndTheOperation()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, Jun1, Jun30);
        h.SeedFollowingCoverage(h.SeedDemandOn(phase));

        var replan = Refused(await Projects(h).ReplanAsync(phase, new ReplanDto { Start = Jun10, End = Jun30 }),
            ProblemCode.NodeHasFollowers);
        replan.Args!["count"].Should().Be(2); // one block, both edges follow
        replan.Args["operation"].Should().Be("replan");

        var rollUp = Refused(await Projects(h).RecalculatePlannedFromChildrenAsync(phase),
            ProblemCode.NodeHasFollowers);
        rollUp.Args!["operation"].Should().Be("rollUp");
    }

    [Fact]
    public async Task DeletingANodeInUse_CarriesTheCountsOfTheCheckThatRefused_InOrder()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, Jun1, Jun30);
        h.SeedAllocation(Jun1, Jun10);
        h.SeedConstraint(h.ProjectNodeId, Jun30);
        var projects = Projects(h);

        // Children first: "reparent them" is its own advice.
        var children = Refused(await projects.DeleteAsync(h.ProjectNodeId), ProblemCode.NodeInUse);
        children.Args!["node"].Should().Be("Proj");
        children.Args["children"].Should().Be(1);
        children.Args.Should().NotContainKey("demands");

        h.Db.ProjectNodes.Remove(h.Db.ProjectNodes.Single(p => p.Id == phase));
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        // Then the plan on the node.
        var plan = Refused(await projects.DeleteAsync(h.ProjectNodeId), ProblemCode.NodeInUse);
        plan.Args!["demands"].Should().Be(1);
        plan.Args["coverages"].Should().Be(1);
        plan.Args.Should().NotContainKey("children");

        h.Db.Allocations.RemoveRange(h.Db.Allocations);
        h.Db.Demands.RemoveRange(h.Db.Demands);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        // Then the imposed dates.
        var constraints = Refused(await projects.DeleteAsync(h.ProjectNodeId), ProblemCode.NodeInUse);
        constraints.Args!["constraints"].Should().Be(1);
    }

    [Fact]
    public async Task DeletingARoleInUse_CountsPeopleAndDemands()
    {
        var h = PlanCommandHarness.Create();
        var roles = new RoleService(new Repository<Role, Guid>(h.Db), h.Db, new Mapper());

        var error = Refused(await roles.DeleteAsync(h.RoleId), ProblemCode.RoleInUse);

        error.Args!["role"].Should().Be("Backend");
        error.Args["resources"].Should().Be(0);
        error.Args["demands"].Should().Be(1);
    }

    [Fact]
    public async Task DeletingATeamWithMembers_CountsThem()
    {
        var h = PlanCommandHarness.Create();
        var team = Team.Create("Platform");
        h.Db.Teams.Add(team);
        h.Db.Resources.Single(r => r.Id == h.ResourceId).AssignToTeam(team.Id);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();
        var teams = new TeamService(new Repository<Team, Guid>(h.Db), h.Db, new Mapper());

        var error = Refused(await teams.DeleteAsync(team.Id), ProblemCode.TeamInUse);

        error.Args!["team"].Should().Be("Platform");
        error.Args["resources"].Should().Be(1);
        (await h.Db.Teams.AsNoTracking().AnyAsync(t => t.Id == team.Id)).Should().BeTrue();
    }
}
