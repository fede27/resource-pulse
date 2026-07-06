using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Optional status filter on the capacity-based daily load series (Allocazioni
// GAP 2): the heatmap cells count Hard blocks by default and add tentative on
// request. Twin of the load-profile filter (Phase F4); the calculator stays
// status-agnostic — the narrowing happens in the query service.
public class ResourceLoadStatusFilterTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5); // 5 days × 8h

    private static (LiveLoadQueryService Svc, Guid ResourceId) Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"loadstatus-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var r = Resource.Create("Tizio", Guid.NewGuid());
        var role = Role.Create("Backend");
        var alpha = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, CommitmentLevel.Committed, null);
        var d = Demand.Create(alpha.Id, role.Id, null, DemandProvenance.Declared);
        var hard = Allocation.CreateCoverage(d.Id, alpha.Id, r.Id, Mon, Fri, 50m, status: AllocationStatus.Hard);
        var tentative = Allocation.CreateCoverage(d.Id, alpha.Id, r.Id, Mon, Fri, 30m);

        db.Resources.Add(r);
        db.Roles.Add(role);
        db.ProjectNodes.Add(alpha);
        db.Demands.Add(d);
        db.Allocations.AddRange(hard, tentative);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return (new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))), r.Id);
    }

    [Fact]
    public async Task NoFilter_SumsAllStatuses()
    {
        var (svc, rid) = Seed();

        var result = await svc.GetForResourceAsync(rid, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        var monday = result.Value.Single(x => x.Date == Mon);
        monday.LoadPercent.Should().Be(80m);                    // 50 hard + 30 tentative
        monday.Hours.Should().Be(TimeSpan.FromHours(6.4));      // 80% × 8h
    }

    [Fact]
    public async Task HardOnly_ExcludesTentativeBlocks()
    {
        var (svc, rid) = Seed();

        var result = await svc.GetForResourceAsync(rid, Mon, Fri, AllocationStatus.Hard);

        result.IsSuccess.Should().BeTrue();
        var monday = result.Value.Single(x => x.Date == Mon);
        monday.LoadPercent.Should().Be(50m);
        monday.Hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public async Task TentativeOnly_ExcludesHardBlocks()
    {
        var (svc, rid) = Seed();

        var result = await svc.GetForResourceAsync(rid, Mon, Fri, AllocationStatus.Tentative);

        result.IsSuccess.Should().BeTrue();
        result.Value.Single(x => x.Date == Mon).LoadPercent.Should().Be(30m);
    }
}
