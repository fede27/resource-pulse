using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Results;
using TimeSloth.Domain.Allocations;
using TimeSloth.Domain.Demands;
using TimeSloth.Domain.Projects;
using TimeSloth.Domain.Resources;
using TimeSloth.Domain.Roles;
using TimeSloth.Persistence;
using TimeSloth.Services.ExternalConstraints;

namespace TimeSloth.Application.Tests;

// The anagrafica of imposed dates (ADR-0035 §4): attached to a root, listed
// per root, and FULLY editable. A constraint is not a referent — no coverage
// boundary can follow it — so moving the date moves nothing in the plan, and
// deleting one is a delete. What the aggregate exists for is the negotiation
// delta: the distance between this date and what the plan actually sustains.
public class ExternalConstraintServiceTests
{
    private static readonly DateOnly GoLive = new(2026, 9, 15);

    private static (ExternalConstraintService svc, TimeSlothDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<TimeSlothDbContext>()
            .UseInMemoryDatabase($"ext-{Guid.NewGuid()}")
            .Options;
        var db = new TimeSlothDbContext(options);

        var mapper = new Mapper(new TypeAdapterConfig());

        return (new ExternalConstraintService(new Repository<ExternalConstraint, Guid>(db), db, mapper), db);
    }

    private static (Guid root, Guid phase) SeedProject(TimeSlothDbContext db)
    {
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Grow, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Fase 1", null);
        db.ProjectNodes.AddRange(root, phase);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (root.Id, phase.Id);
    }

    // A block on the root ending exactly ON the imposed date. Under ADR-0034
    // this was an anchor and it froze the constraint's date; now it is a
    // coincidence, and the point of these tests is that it costs nothing.
    private static void SeedBlockEndingOnTheDate(TimeSlothDbContext db, Guid rootId)
    {
        var role = Role.Create("Dev");
        var person = Resource.Create("Tizio", Guid.NewGuid());
        var demand = Demand.Create(rootId, role.Id, null, DemandProvenance.Declared);
        var block = Allocation.CreateCoverage(demand.Id, rootId, person.Id, new DateOnly(2026, 6, 1), GoLive, 50m);
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
    public async Task Update_MovingTheDate_MovesNoCoverage_EvenOneEndingExactlyOnIt()
    {
        // The renegotiation gesture: the customer moved the deadline. The plan
        // does not chase it — the DELTA moves, and that is the thing worth
        // seeing. Under ADR-0034 this call was a 409.
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedBlockEndingOnTheDate(db, root);

        var result = await svc.UpdateAsync(root, created.Value.Id, new UpdateExternalConstraintDto
        {
            Name = "Go-live", Date = GoLive.AddDays(30), Authority = ConstraintAuthority.Contractual
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ExternalConstraints.AsNoTracking().Single(c => c.Id == created.Value.Id).Date
            .Should().Be(GoLive.AddDays(30));
        db.Allocations.AsNoTracking().Single().PeriodEnd.Should().Be(GoLive);
    }

    [Fact]
    public async Task Update_RenamingAndReweighing_IsAllowed()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedBlockEndingOnTheDate(db, root);

        var result = await svc.UpdateAsync(root, created.Value.Id, new UpdateExternalConstraintDto
        {
            Name = "Go-live (rinegoziato)", Date = GoLive, Authority = ConstraintAuthority.Desiderata
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Authority.Should().Be(ConstraintAuthority.Desiderata);
    }

    [Fact]
    public async Task Update_MovingADateWithNoCoverageAtAll_IsAllowed()
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
    public async Task Delete_IsNeverBlockedByCoverage()
    {
        // Nothing references a constraint any more: ADR-0035 dropped the FK
        // from allocations, so there is no dependant to refuse for.
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());
        SeedBlockEndingOnTheDate(db, root);

        var result = await svc.DeleteAsync(root, created.Value.Id);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ExternalConstraints.AsNoTracking().Any(c => c.Id == created.Value.Id).Should().BeFalse();
        db.Allocations.AsNoTracking().Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_Deletes()
    {
        var (svc, db) = Build();
        var (root, _) = SeedProject(db);
        var created = await svc.CreateAsync(root, Create());

        var result = await svc.DeleteAsync(root, created.Value.Id);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.ExternalConstraints.AsNoTracking().Any().Should().BeFalse();
    }
}
