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

// Deleting a project node. Three Restrict FKs point at project_nodes — parent_id,
// demands.project_node_id and allocations.project_node_id — and only the first was
// checked, so a node carrying a demand or a coverage block failed at SaveChanges
// and surfaced as a 500 for what is a plain conflict.
//
// The InMemory provider does not enforce FKs, which is precisely why these tests
// are about the SERVICE's checks: they are the thing that has to notice.
public class ProjectNodeDeleteTests
{
    private static (ProjectNodeService svc, ResourcePulseDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"pn-del-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var config = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(config);
        var mapper = new Mapper(config);

        var policy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db), db);

        return (new ProjectNodeService(new Repository<ProjectNode, Guid>(db), db, mapper, policy), db);
    }

    private static async Task<Guid> NewProjectAsync(ProjectNodeService svc, string name = "Apollo") =>
        (await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Project,
            Name = name,
            Type = ProjectType.Customer,
            CommitmentLevel = CommitmentLevel.Committed
        })).Value.Id;

    private static Guid SeedDemand(ResourcePulseDbContext db, Guid nodeId)
    {
        var role = Role.Create("Dev");
        var demand = Demand.Create(nodeId, role.Id, TimeSpan.FromHours(40), DemandProvenance.Declared);
        db.Roles.Add(role);
        db.Demands.Add(demand);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return demand.Id;
    }

    private static void SeedCoverage(ResourcePulseDbContext db, Guid nodeId, Guid demandId)
    {
        var resource = Resource.Create("Tizio", Guid.NewGuid());
        var coverage = Allocation.CreateCoverage(
            demandId, nodeId, resource.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), 50m);
        db.Resources.Add(resource);
        db.Allocations.Add(coverage);
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ANodeCarryingNothing_IsDeleted()
    {
        var (svc, db) = Build();
        var id = await NewProjectAsync(svc);

        var result = await svc.DeleteAsync(id);

        result.IsSuccess.Should().BeTrue();
        db.ProjectNodes.Any(n => n.Id == id).Should().BeFalse();
    }

    [Fact]
    public async Task ANodeWithADemand_IsAConflictAndNotAFault()
    {
        var (svc, db) = Build();
        var id = await NewProjectAsync(svc);
        SeedDemand(db, id);

        var result = await svc.DeleteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 demand");
        db.ProjectNodes.Any(n => n.Id == id).Should().BeTrue();
    }

    [Fact]
    public async Task ANodeWithCoverage_IsAConflictAndNotAFault()
    {
        var (svc, db) = Build();
        var id = await NewProjectAsync(svc);
        var demandId = SeedDemand(db, id);
        SeedCoverage(db, id, demandId);

        var result = await svc.DeleteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage block");
    }

    [Fact]
    public async Task ChildrenStillTakePrecedence()
    {
        // The pre-existing check must keep its own message: "reparent the children"
        // is different advice from "clear the plan off this node".
        var (svc, _) = Build();
        var rootId = await NewProjectAsync(svc);
        await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Phase,
            Name = "Phase 1",
            ParentId = rootId
        });

        var result = await svc.DeleteAsync(rootId);

        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("children");
    }

    [Fact]
    public async Task OnceThePlanIsCleared_TheNodeDeletes()
    {
        var (svc, db) = Build();
        var id = await NewProjectAsync(svc);
        var demandId = SeedDemand(db, id);
        SeedCoverage(db, id, demandId);

        (await svc.DeleteAsync(id)).IsFailure.Should().BeTrue();

        db.Allocations.RemoveRange(db.Allocations.Where(a => a.ProjectNodeId == id));
        db.Demands.RemoveRange(db.Demands.Where(d => d.ProjectNodeId == id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await svc.DeleteAsync(id)).IsSuccess.Should().BeTrue();
    }
}
