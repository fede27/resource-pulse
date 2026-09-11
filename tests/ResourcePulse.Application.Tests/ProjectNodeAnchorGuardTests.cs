using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Application.Tests;

// The project endpoints are the anagrafica side of ADR-0034 §5: a node whose
// dates have anchored dependants is NOT replanned, rolled up or deleted from
// here — they refuse with the count and point at the envelope. A node with no
// dependants behaves exactly as before. The read DTO carries the count so the
// client knows which case it is in before the gesture.
public class ProjectNodeAnchorGuardTests
{
    private static readonly DateOnly PhaseStart = new(2026, 6, 8);
    private static readonly DateOnly PhaseEnd = new(2026, 6, 30);

    private static (ProjectNodeService svc, ResourcePulseDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"pn-anchor-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var config = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(config);
        var mapper = new Mapper(config);

        var policy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db), db);

        return (new ProjectNodeService(new Repository<ProjectNode, Guid>(db), db, mapper, policy), db);
    }

    // A root with a dated phase, and a coverage on the ROOT whose end is anchored
    // to the PHASE's end. Returns (rootId, phaseId).
    private static (Guid root, Guid phase) SeedAnchoredPlan(ResourcePulseDbContext db)
    {
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Customer, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Fase 1", null);
        phase.Replan(PhaseStart, PhaseEnd);
        var role = Role.Create("Dev");
        var resource = Resource.Create("Tizio", Guid.NewGuid());
        var demand = Demand.Create(root.Id, role.Id, null, DemandProvenance.Declared);
        var coverage = Allocation.CreateCoverage(demand.Id, root.Id, resource.Id, new DateOnly(2026, 6, 1), PhaseEnd, 50m);
        coverage.Anchor(BoundaryEdge.End, BoundaryAnchor.ToNodeEnd(phase.Id), PhaseEnd);

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
    public async Task Replan_OnANodeWithAnchoredDependants_IsAConflict_ThatNamesTheEnvelope()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedAnchoredPlan(db);

        var result = await svc.ReplanAsync(phase, new ReplanDto { Start = PhaseStart, End = PhaseEnd.AddDays(5) });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage boundary(ies)").And.Contain("replanNode");
        db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task Replan_OnANodeWithoutDependants_WorksAsBefore()
    {
        var (svc, db) = Build();
        var (root, _) = SeedAnchoredPlan(db); // the ROOT has no anchored dependants (the anchor targets the phase)

        var result = await svc.ReplanAsync(root, new ReplanDto { Start = PhaseStart, End = PhaseEnd });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ProjectNodes.AsNoTracking().Single(p => p.Id == root).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task RecalculatePlannedFromChildren_OnANodeWithAnchoredDependants_IsAConflict()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedAnchoredPlan(db);

        var result = await svc.RecalculatePlannedFromChildrenAsync(phase);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }

    [Fact]
    public async Task Delete_OnANodeOtherBlocksAreAnchoredTo_IsAConflict_NotAFault()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedAnchoredPlan(db); // the phase carries nothing itself; the block on the root points at it

        var result = await svc.DeleteAsync(phase);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("anchored");
        db.ProjectNodes.AsNoTracking().Any(p => p.Id == phase).Should().BeTrue();
    }

    [Fact]
    public async Task ReadDto_CarriesTheAnchoredEdgeCount()
    {
        var (svc, db) = Build();
        var (root, phase) = SeedAnchoredPlan(db);

        var phaseDto = await svc.GetByIdAsync(phase);
        var rootDto = await svc.GetByIdAsync(root);
        var subtree = await svc.GetSubtreeAsync(root);

        phaseDto.Value.AnchoredEdgeCount.Should().Be(1);
        rootDto.Value.AnchoredEdgeCount.Should().Be(0);
        subtree.Value.Single(n => n.Id == phase).AnchoredEdgeCount.Should().Be(1);
    }
}
