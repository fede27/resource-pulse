using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.ExternalConstraints;

namespace ResourcePulse.Application.Tests;

// The anagrafica of imposed dates (ADR-0034 §4): attached to a root, listed
// per root, editable — except the date once boundaries follow it, which is
// refused with the count and pointed at moveConstraint. Deleting a referent
// with anchored dependants is a conflict, not a fault.
public class ExternalConstraintServiceTests
{
    private static readonly DateOnly GoLive = new(2026, 9, 15);

    private static (ExternalConstraintService svc, ResourcePulseDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"ext-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var config = new TypeAdapterConfig();
        new ExternalConstraintMappingRegister().Register(config);
        var mapper = new Mapper(config);

        return (new ExternalConstraintService(new Repository<ExternalConstraint, Guid>(db), db, mapper), db);
    }

    private static (Guid root, Guid phase) SeedProject(ResourcePulseDbContext db)
    {
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Customer, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Fase 1", null);
        db.ProjectNodes.AddRange(root, phase);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (root.Id, phase.Id);
    }

    // A block on the root whose end is anchored to `constraintId`.
    private static void SeedAnchoredBlock(ResourcePulseDbContext db, Guid rootId, Guid constraintId)
    {
        var role = Role.Create("Dev");
        var person = Resource.Create("Tizio", Guid.NewGuid());
        var demand = Demand.Create(rootId, role.Id, null, DemandProvenance.Declared);
        var block = Allocation.CreateCoverage(demand.Id, rootId, person.Id, new DateOnly(2026, 6, 1), GoLive, 50m);
        block.Anchor(BoundaryEdge.End, BoundaryAnchor.ToExternal(constraintId), GoLive);
        db.Roles.Add(role);
        db.Resources.Add(person);
        db.Demands.Add(demand);
        db.Allocations.Add(block);
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static CreateExternalConstraintDto Create(string name = "Go-live") => new()
    {
        Name = name, Date = GoLive, Authority = ConstraintAuthority.Contractual, Notes = "penale 2%"
    };

    [Fact]
    public async Task Create_OnARoot_ThenListAndGet()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);

        var created = await svc.CreateAsync(root, Create());
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        created.Value.Authority.Should().Be(ConstraintAuthority.Contractual);
        created.Value.AnchoredEdgeCount.Should().Be(0);

        var list = await svc.GetForRootAsync(root);
        list.Value.Should().ContainSingle().Which.Id.Should().Be(created.Value.Id);

        var one = await svc.GetByIdAsync(root, created.Value.Id);
        one.Value.Name.Should().Be("Go-live");
    }

    [Fact]
    public async Task Create_OnAPhase_IsAValidationError_ConstraintsAttachToRoots()
    {
        var (svc, db) = Build();
        var (_, phase) = SeedProject(db);

        var result = await svc.CreateAsync(phase, Create());

        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
    }

    [Fact]
    public async Task GetById_UnderTheWrongRoot_IsNotFound()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());

        var result = await svc.GetByIdAsync(Guid.NewGuid(), created.Value.Id);

        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task Update_MovingADateBoundariesFollow_IsAConflict_ThatNamesTheEnvelope()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedAnchoredBlock(db, root, created.Value.Id);

        var result = await svc.UpdateAsync(root, created.Value.Id, new UpdateExternalConstraintDto
        {
            Name = "Go-live", Date = GoLive.AddDays(30), Authority = ConstraintAuthority.Contractual
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage boundary(ies)").And.Contain("moveConstraint");
        db.ExternalConstraints.AsNoTracking().Single(c => c.Id == created.Value.Id).Date.Should().Be(GoLive);
    }

    [Fact]
    public async Task Update_RenamingAndReweighing_WithTheSameDate_IsAllowedEvenWhenFollowed()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedAnchoredBlock(db, root, created.Value.Id);

        var result = await svc.UpdateAsync(root, created.Value.Id, new UpdateExternalConstraintDto
        {
            Name = "Go-live (rinegoziato)", Date = GoLive, Authority = ConstraintAuthority.Desiderata
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Authority.Should().Be(ConstraintAuthority.Desiderata);
        result.Value.AnchoredEdgeCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_MovingADateNothingFollows_IsAllowed()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());

        var result = await svc.UpdateAsync(root, created.Value.Id, new UpdateExternalConstraintDto
        {
            Name = "Go-live", Date = GoLive.AddDays(30), Authority = ConstraintAuthority.Contractual
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Date.Should().Be(GoLive.AddDays(30));
    }

    [Fact]
    public async Task Delete_WithAnchoredDependants_IsAConflict_NotAFault()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedAnchoredBlock(db, root, created.Value.Id);

        var result = await svc.DeleteAsync(root, created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        db.ExternalConstraints.AsNoTracking().Any(c => c.Id == created.Value.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNothingAnchored_Deletes()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());

        var result = await svc.DeleteAsync(root, created.Value.Id);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ExternalConstraints.AsNoTracking().Any().Should().BeFalse();
    }
}
