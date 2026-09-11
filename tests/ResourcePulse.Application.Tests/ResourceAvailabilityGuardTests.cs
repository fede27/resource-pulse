using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Resources;

namespace ResourcePulse.Application.Tests;

// The anagrafica side of the availability referent (ADR-0034 §5): PUT on a
// resource may set the boundary freely while nothing follows it, and refuses
// with the count — per edge — once a coverage boundary is anchored to it.
public class ResourceAvailabilityGuardTests
{
    private static readonly DateOnly Until = new(2026, 7, 31);

    private sealed class AnonymousAccessor : ICurrentUserAccessor
    {
        public CurrentUser User => CurrentUser.Anonymous;
        public bool IsAuthenticated => false;
        public string? AuthenticationScheme => null;
    }

    private static (ResourceService svc, ResourcePulseDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"res-avail-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var config = new TypeAdapterConfig();
        config.Scan(typeof(ResourceMappingRegister).Assembly);
        var mapper = new Mapper(config);

        var svc = new ResourceService(
            new Repository<Resource, Guid>(db), db, mapper, new AnonymousAccessor(), TimeProvider.System);
        return (svc, db);
    }

    // A calendar, a person with AvailableUntil, a root+demand, and a block whose
    // end is anchored to the person's availability. Returns (resourceId, calendarId).
    private static (Guid resource, Guid calendar) SeedAnchoredPlan(ResourcePulseDbContext db, bool anchored = true)
    {
        var calendar = BusinessCalendar.Create("Std", isDefault: true);
        var person = Resource.Create("Tizio", calendar.Id);
        person.SetAvailability(null, Until);
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Customer, CommitmentLevel.Committed, null);
        var role = Role.Create("Dev");
        var demand = Demand.Create(root.Id, role.Id, null, DemandProvenance.Declared);
        var block = Allocation.CreateCoverage(demand.Id, root.Id, person.Id, new DateOnly(2026, 6, 1), Until, 50m);
        if (anchored) block.Anchor(BoundaryEdge.End, BoundaryAnchor.ToResourceAvailability(), Until);

        db.BusinessCalendars.Add(calendar);
        db.Resources.Add(person);
        db.ProjectNodes.Add(root);
        db.Roles.Add(role);
        db.Demands.Add(demand);
        db.Allocations.Add(block);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (person.Id, calendar.Id);
    }

    private static UpdateResourceDto Update(Guid calendar, DateOnly? from, DateOnly? until) => new()
    {
        Name = "Tizio", IsActive = true, BusinessCalendarId = calendar, AvailableFrom = from, AvailableUntil = until
    };

    [Fact]
    public async Task Update_ChangingAFollowedBoundary_IsAConflict_ThatNamesTheEnvelope()
    {
        var (svc, db) = Build();
        var (person, calendar) = SeedAnchoredPlan(db);

        var result = await svc.UpdateAsync(person, Update(calendar, from: null, until: Until.AddDays(30)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error.Message.Should().Contain("1 coverage boundary(ies)").And.Contain("setAvailability");
        db.Resources.AsNoTracking().Single(r => r.Id == person).AvailableUntil.Should().Be(Until);
    }

    [Fact]
    public async Task Update_ChangingTheOtherEdge_IsAllowed_TheCountIsPerEdge()
    {
        var (svc, db) = Build();
        var (person, calendar) = SeedAnchoredPlan(db); // only the END is followed

        var result = await svc.UpdateAsync(person, Update(calendar, from: new DateOnly(2026, 1, 1), until: Until));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.Resources.AsNoTracking().Single(r => r.Id == person).AvailableFrom.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public async Task Update_WithNothingFollowing_SetsTheBoundaryAsBefore()
    {
        var (svc, db) = Build();
        var (person, calendar) = SeedAnchoredPlan(db, anchored: false);

        var result = await svc.UpdateAsync(person, Update(calendar, from: null, until: Until.AddDays(30)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.Resources.AsNoTracking().Single(r => r.Id == person).AvailableUntil.Should().Be(Until.AddDays(30));
    }

    [Fact]
    public async Task ReadDto_CarriesTheBoundaryAndTheAnchoredEdgeCount()
    {
        var (svc, db) = Build();
        var (person, _) = SeedAnchoredPlan(db);

        var dto = await svc.GetByIdAsync(person);

        dto.IsSuccess.Should().BeTrue(dto.Error?.Message);
        dto.Value.AvailableUntil.Should().Be(Until);
        dto.Value.AnchoredEdgeCount.Should().Be(1);
    }
}
