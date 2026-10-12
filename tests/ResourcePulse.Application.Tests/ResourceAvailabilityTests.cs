using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Resources;

namespace ResourcePulse.Application.Tests;

// Declared availability after ADR-0035 §3. It is NOT a referent: nothing
// follows it, so PUT on a resource never refuses, never drags a block, and
// never sends the editor to a plan gesture. This file exists to keep that
// property nailed down — under ADR-0034 every one of these edits was a 409 with
// a count, and it fell on whoever maintains the anagrafica rather than on the
// person who plans.
public class ResourceAvailabilityTests
{
    private static readonly DateOnly Until = new(2026, 7, 31);
    private static readonly DateOnly BlockStart = new(2026, 6, 1);

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

    // A calendar, a person with AvailableUntil, a root+demand, and a coverage
    // running exactly to that boundary — the shape that used to be blocking.
    private static (Guid resource, Guid calendar, Guid block) SeedPlan(ResourcePulseDbContext db)
    {
        var calendar = BusinessCalendar.Create("Std", isDefault: true);
        var person = Resource.Create("Tizio", calendar.Id);
        person.SetAvailability(null, Until);
        var root = ProjectNode.CreateRoot("Apollo", "AP", ProjectType.Customer, CommitmentLevel.Committed, null);
        root.Replan(BlockStart, Until);
        var role = Role.Create("Dev");
        var demand = Demand.Create(root.Id, role.Id, null, DemandProvenance.Declared);
        var block = Allocation.CreateCoverage(demand.Id, root.Id, person.Id, BlockStart, Until, 50m);
        block.FollowNode(BoundaryEdge.End, Until);

        db.BusinessCalendars.Add(calendar);
        db.Resources.Add(person);
        db.ProjectNodes.Add(root);
        db.Roles.Add(role);
        db.Demands.Add(demand);
        db.Allocations.Add(block);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return (person.Id, calendar.Id, block.Id);
    }

    private static UpdateResourceDto Update(Guid calendar, DateOnly? from, DateOnly? until) => new()
    {
        Name = "Tizio", IsActive = true, BusinessCalendarId = calendar, AvailableFrom = from, AvailableUntil = until
    };

    [Fact]
    public async Task Update_MovingTheAvailabilityBoundary_Succeeds_AndMovesNoBlock()
    {
        var (svc, db) = Build();
        var (person, calendar, block) = SeedPlan(db);

        var result = await svc.UpdateAsync(person, Update(calendar, from: null, until: Until.AddDays(30)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.Resources.AsNoTracking().Single(r => r.Id == person).AvailableUntil.Should().Be(Until.AddDays(30));
        db.Allocations.AsNoTracking().Single(a => a.Id == block).PeriodEnd.Should().Be(Until);
    }

    [Fact]
    public async Task Update_ClearingTheAvailabilityBoundary_Succeeds()
    {
        // The worst case under ADR-0034: clearing a followed date was refused
        // outright. Now it is a field going null.
        var (svc, db) = Build();
        var (person, calendar, block) = SeedPlan(db);

        var result = await svc.UpdateAsync(person, Update(calendar, from: null, until: null));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        db.Resources.AsNoTracking().Single(r => r.Id == person).AvailableUntil.Should().BeNull();
        db.Allocations.AsNoTracking().Single(a => a.Id == block).PeriodEnd.Should().Be(Until);
    }

    [Fact]
    public async Task Update_PullingAvailabilityBeforeAnExistingBlock_IsAllowed_TheViolationIsVisibleNotRefused()
    {
        // Coverage past AvailableUntil is a violation the board HATCHES, not a
        // block the model refuses or shortens. Capacity does not read these
        // dates at all, so the plan's arithmetic is unchanged.
        var (svc, db) = Build();
        var (person, calendar, block) = SeedPlan(db);

        var result = await svc.UpdateAsync(person, Update(calendar, from: null, until: new DateOnly(2026, 6, 15)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var stored = db.Allocations.AsNoTracking().Single(a => a.Id == block);
        stored.PeriodEnd.Should().Be(Until);
        stored.EndAnchor.Should().Be(AnchorKind.FollowsNode);
    }

    [Fact]
    public async Task ReadDto_CarriesTheBoundary_AndNoLongerCarriesAnEdgeCount()
    {
        var (svc, db) = Build();
        var (person, _, _) = SeedPlan(db);

        var dto = await svc.GetByIdAsync(person);

        dto.IsSuccess.Should().BeTrue(dto.Error?.Message);
        dto.Value.AvailableUntil.Should().Be(Until);
        typeof(ResourceReadDto).GetProperty("AnchoredEdgeCount").Should().BeNull(
            "availability stopped being a referent: a count of what follows it would have no meaning");
    }
}
