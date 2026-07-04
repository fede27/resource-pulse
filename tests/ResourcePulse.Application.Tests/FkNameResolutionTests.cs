using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Application.Tests;

// FK→name resolution on existing read DTOs (gap #7 / ADR-0024):
//   - AllocationReadDto.ResourceRoleId/Name = the assigned person's Role.
//   - ProjectNodeReadDto.LeadResourceName   = the project owner (PM) name.
public class FkNameResolutionTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D5 = new(2026, 6, 5);

    private static ResourcePulseDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"fkname-{Guid.NewGuid()}")
            .Options);

    // ── AllocationReadDto: assigned person's role ───────────────────────────────

    [Fact]
    public async Task Coverage_ResolvesResourceRole_AndDemandRole()
    {
        var db = NewDb();
        var devRole = Role.Create("Dev senior");
        var r = Resource.Create("Tizio", Guid.NewGuid());
        r.AssignToRole(devRole.Id);
        var node = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var demand = Demand.Create(node.Id, devRole.Id, null, DemandProvenance.Declared);
        var alloc = Allocation.CreateCoverage(demand.Id, node.Id, r.Id, D1, D5, 50m);

        db.Roles.Add(devRole);
        db.Resources.Add(r);
        db.ProjectNodes.Add(node);
        db.Demands.Add(demand);
        db.Allocations.Add(alloc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new AllocationService(db, new FixedCapacity(TimeSpan.FromHours(8)));
        var result = await svc.GetForProjectNodeAsync(node.Id, D1, D5);

        var dto = result.Value.Should().ContainSingle().Subject;
        dto.ResourceRoleId.Should().Be(devRole.Id);
        dto.ResourceRoleName.Should().Be("Dev senior");
        dto.DemandRoleId.Should().Be(devRole.Id);
        dto.DemandRoleName.Should().Be("Dev senior");
    }

    [Fact]
    public async Task Coverage_ResourceWithoutRole_HasNullResourceRole()
    {
        var db = NewDb();
        var role = Role.Create("Backend");
        var r = Resource.Create("Tizio", Guid.NewGuid()); // no role
        var node = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var demand = Demand.Create(node.Id, role.Id, null, DemandProvenance.Declared);
        var alloc = Allocation.CreateCoverage(demand.Id, node.Id, r.Id, D1, D5, 50m);

        db.Roles.Add(role);
        db.Resources.Add(r);
        db.ProjectNodes.Add(node);
        db.Demands.Add(demand);
        db.Allocations.Add(alloc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new AllocationService(db, new FixedCapacity(TimeSpan.FromHours(8)));
        var dto = (await svc.GetForProjectNodeAsync(node.Id, D1, D5)).Value.Single();

        dto.ResourceRoleId.Should().BeNull();
        dto.ResourceRoleName.Should().BeNull();
    }

    [Fact]
    public async Task Coverage_RoleMismatch_IsVisible_NotRewritten()
    {
        // The demand asks a Designer; a Dev covers it. Both roles are exposed side
        // by side — the "storto" match is surfaced, not enforced (revision §6).
        var db = NewDb();
        var demandRole = Role.Create("Designer");
        var personRole = Role.Create("Dev senior");
        var r = Resource.Create("Tizio", Guid.NewGuid());
        r.AssignToRole(personRole.Id);
        var node = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var demand = Demand.Create(node.Id, demandRole.Id, null, DemandProvenance.Declared);
        var alloc = Allocation.CreateCoverage(demand.Id, node.Id, r.Id, D1, D5, 50m);

        db.Roles.AddRange(demandRole, personRole);
        db.Resources.Add(r);
        db.ProjectNodes.Add(node);
        db.Demands.Add(demand);
        db.Allocations.Add(alloc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new AllocationService(db, new FixedCapacity(TimeSpan.FromHours(8)));
        var dto = (await svc.GetForProjectNodeAsync(node.Id, D1, D5)).Value.Single();

        dto.DemandRoleName.Should().Be("Designer");     // what was asked
        dto.ResourceRoleName.Should().Be("Dev senior"); // who actually covers it
        dto.DemandRoleName.Should().NotBe(dto.ResourceRoleName); // mismatch visible
    }

    // ── ProjectNodeReadDto: owner (PM) name ─────────────────────────────────────

    private static ProjectNodeService NewProjectSvc(ResourcePulseDbContext db)
    {
        var config = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(config);
        var policy = new CommitmentPolicyService(new Repository<CommitmentPolicyConfiguration, Guid>(db));
        return new ProjectNodeService(new Repository<ProjectNode, Guid>(db), db, new Mapper(config), policy);
    }

    [Fact]
    public async Task Project_ResolvesLeadResourceName_OnCreateAndRead()
    {
        var db = NewDb();
        var pm = Resource.Create("Paola PM", Guid.NewGuid());
        db.Resources.Add(pm);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = NewProjectSvc(db);

        var created = await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Project,
            Name = "Apollo",
            Type = ProjectType.Customer,
            CommitmentLevel = CommitmentLevel.Committed,
            LeadResourceId = pm.Id
        });

        created.IsSuccess.Should().BeTrue();
        created.Value.LeadResourceId.Should().Be(pm.Id);
        created.Value.LeadResourceName.Should().Be("Paola PM");

        var read = await svc.GetByIdAsync(created.Value.Id);
        read.Value.LeadResourceName.Should().Be("Paola PM");
    }

    [Fact]
    public async Task Project_NoLead_LeavesLeadResourceNameNull()
    {
        var db = NewDb();
        var svc = NewProjectSvc(db);

        var created = await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Project,
            Name = "Apollo",
            Type = ProjectType.Internal,
            CommitmentLevel = CommitmentLevel.Planned
        });

        created.Value.LeadResourceId.Should().BeNull();
        created.Value.LeadResourceName.Should().BeNull();
    }

    [Fact]
    public async Task ProjectsInRange_ResolveLeadName()
    {
        var db = NewDb();
        var pm = Resource.Create("Paola PM", Guid.NewGuid());
        db.Resources.Add(pm);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = NewProjectSvc(db);
        var created = await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Project,
            Name = "Apollo",
            Type = ProjectType.Customer,
            CommitmentLevel = CommitmentLevel.Committed,
            LeadResourceId = pm.Id
        });
        // Give it planned dates so the range filter matches.
        await svc.ReplanAsync(created.Value.Id, new ReplanDto { Start = D1, End = D5 });

        var list = await svc.GetProjectsActiveInRangeAsync(D1, D5, DateSource.Planned);

        list.Value.Should().ContainSingle()
            .Which.LeadResourceName.Should().Be("Paola PM");
    }
}
