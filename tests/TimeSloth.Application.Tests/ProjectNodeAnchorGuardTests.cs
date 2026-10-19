using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Results;
using TimeSloth.Domain.Allocations;
using TimeSloth.Domain.Configuration;
using TimeSloth.Domain.Demands;
using TimeSloth.Domain.Projects;
using TimeSloth.Domain.Resources;
using TimeSloth.Domain.Roles;
using TimeSloth.Persistence;
using TimeSloth.Services.Configuration;
using TimeSloth.Services.Projects;

namespace TimeSloth.Application.Tests;

// The project endpoints are the anagrafica side of ADR-0035 §5 — and, since
// that ADR, the ONLY anagrafica surface that still refuses anything. A node
// whose dates are followed is not replanned, rolled up or deleted from here: it
// refuses with the count and points at the envelope. A node nothing follows
// behaves exactly as before. The read DTO carries the count — on the node, and
// now only on the node — so the client knows which case it is in beforehand.
public class ProjectNodeAnchorGuardTests
{
    private static readonly DateOnly PhaseStart = new(2026, 6, 8);
    private static readonly DateOnly PhaseEnd = new(2026, 6, 30);

    private static (ProjectNodeService svc, TimeSlothDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<TimeSlothDbContext>()
            .UseInMemoryDatabase($"pn-anchor-{Guid.NewGuid()}")
            .Options;
        var db = new TimeSlothDbContext(options);

        var config = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(config);
        var mapper = new Mapper(config);

        var policy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db), db);

        return (new ProjectNodeService(new Repository<ProjectNode, Guid>(db), db, mapper, policy), db);
    }

    // A root with a dated phase and a coverage ON THAT PHASE whose end follows
    // it. Under ADR-0035 a block can only follow the node it sits on, so "the
    // coverage is on the phase" and "the phase is its referent" are one fact.
    // `following` false leaves both edges pinned instead.
    private static (Guid root, Guid phase) SeedPlan(TimeSlothDbContext db, bool following = true)
    {
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Grow, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Fase 1", null);
        phase.Replan(PhaseStart, PhaseEnd);
        var role = Role.Create("Dev");
        var resource = Resource.Create("Tizio", Guid.NewGuid());
        var demand = Demand.Create(phase.Id, role.Id, null, DemandProvenance.Declared);
        var coverage = Allocation.CreateCoverage(demand.Id, phase.Id, resource.Id, new DateOnly(2026, 6, 1), PhaseEnd, 50m);
        if (following) coverage.FollowNode(BoundaryEdge.End, PhaseEnd);

        db.ProjectNodes.AddRange(root, phase);
        db.Roles.Add(role);
        db.Resources.Add(resource);
        db.Demands.Add(demand);
        db.Allocations.Add(coverage);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (root.Id, phase.Id);
    }

    [Fact]
    public async Task Replan_OnANodeWithFollowingBoundaries_IsAConflict_ThatNamesTheEnvelope()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedPlan(db);

        var result = await svc.ReplanAsync(phase, new ReplanDto { Start = PhaseStart, End = PhaseEnd.AddDays(5) });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage boundary(ies)").And.Contain("replanNode");
        db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task Replan_WhenEveryBoundaryOnTheNodeIsPinned_WorksAsBefore()
    {
        // Nothing follows the node, so moving it moves nothing: no reason to
        // route the planner through the envelope.
        var (svc, db) = Build();
        var (_, phase) = SeedPlan(db, following: false);

        var result = await svc.ReplanAsync(phase, new ReplanDto { Start = PhaseStart, End = PhaseEnd.AddDays(5) });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd.AddDays(5));
    }

    [Fact]
    public async Task Replan_OnANodeWithoutCoverage_WorksAsBefore()
    {
        var (svc, db) = Build();
        var (root, _) = SeedPlan(db); // the coverage sits on the phase, not the root

        var result = await svc.ReplanAsync(root, new ReplanDto { Start = PhaseStart, End = PhaseEnd });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ProjectNodes.AsNoTracking().Single(p => p.Id == root).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task RecalculatePlannedFromChildren_OnANodeWithFollowingBoundaries_IsAConflict()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedPlan(db);

        var result = await svc.RecalculatePlannedFromChildrenAsync(phase);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }

    [Fact]
    public async Task Delete_OnANodeThatCarriesCoverage_IsAConflict_NotAFault()
    {
        // Under ADR-0035 there is no separate "something follows this node"
        // refusal on delete: a follower is a coverage ON the node, so the
        // coverage count refuses first and says something more useful.
        var (svc, db) = Build();
        var (_, phase) = SeedPlan(db);

        var result = await svc.DeleteAsync(phase);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage block(s)");
        db.ProjectNodes.AsNoTracking().Any(p => p.Id == phase).Should().BeTrue();
    }

    [Fact]
    public async Task ReadDto_CarriesTheFollowingEdgeCount()
    {
        var (svc, db) = Build();
        var (root, phase) = SeedPlan(db);

        var phaseDto = await svc.GetByIdAsync(phase);
        var rootDto = await svc.GetByIdAsync(root);
        var subtree = await svc.GetSubtreeAsync(root);

        phaseDto.Value.AnchoredEdgeCount.Should().Be(1);
        rootDto.Value.AnchoredEdgeCount.Should().Be(0);
        subtree.Value.Single(n => n.Id == phase).AnchoredEdgeCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadDto_CountsBothEdgesOfABlockThatFollowsOnBothSides()
    {
        // The count is of BOUNDARIES that would move, not of blocks — that is
        // what the preview shows and what the ribbon badge means.
        var (svc, db) = Build();
        var (_, phase) = SeedPlan(db);
        var block = db.Allocations.Single();
        block.FollowNode(BoundaryEdge.Start, PhaseStart);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var dto = await svc.GetByIdAsync(phase);

        dto.Value.AnchoredEdgeCount.Should().Be(2);
    }
}
