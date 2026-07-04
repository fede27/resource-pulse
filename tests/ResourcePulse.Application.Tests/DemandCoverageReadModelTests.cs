using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Demand-coverage read model end-to-end (Phase 5.2, ADR-0025/0026): node/subtree
// gap, best-effort null gap, and the role-mismatch coverage still counting (§6).
public class DemandCoverageReadModelTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5); // 5 days

    private sealed class Fixture
    {
        public LiveLoadQueryService Svc { get; init; } = null!;
        public Guid RootId { get; init; }
        public Guid TargetedDemandId { get; init; }
        public Guid BestEffortDemandId { get; init; }
    }

    // Root(Project) → Phase. A targeted demand on Root (40h) covered 20h; a
    // best-effort demand on the Phase covered by a role-mismatched resource.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"demandcov-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var cal = Guid.NewGuid();
        var dev = Role.Create("Dev");
        var designer = Role.Create("Designer");
        var tizio = Resource.Create("Tizio", cal);
        tizio.AssignToRole(dev.Id);

        var root = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Phase 1", null);

        var targeted = Demand.Create(root.Id, dev.Id, TimeSpan.FromHours(40), DemandProvenance.Declared);
        var bestEffort = Demand.Create(phase.Id, designer.Id, null, DemandProvenance.Declared);

        // Root demand: 50% of 8h × 5d = 20h covered (of 40h ⇒ gap 20h).
        var c1 = Allocation.CreateCoverage(targeted.Id, root.Id, tizio.Id, Mon, Fri, 50m);
        // Best-effort Phase demand covered by a Dev (role mismatch vs Designer).
        var c2 = Allocation.CreateCoverage(bestEffort.Id, phase.Id, tizio.Id, Mon, Fri, 25m);

        db.Roles.AddRange(dev, designer);
        db.Resources.Add(tizio);
        db.ProjectNodes.AddRange(root, phase);
        db.Demands.AddRange(targeted, bestEffort);
        db.Allocations.AddRange(c1, c2);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture
        {
            Svc = new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            RootId = root.Id,
            TargetedDemandId = targeted.Id,
            BestEffortDemandId = bestEffort.Id
        };
    }

    [Fact]
    public async Task Subtree_ReconcilesTargetedAndBestEffortDemands()
    {
        var f = Seed();

        var result = await f.Svc.GetDemandCoverageForProjectNodeAsync(f.RootId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // root + phase demand (subtree)

        var targeted = result.Value.Single(d => d.DemandId == f.TargetedDemandId);
        targeted.RequiredHours.Should().Be(TimeSpan.FromHours(40));
        targeted.CoveredHours.Should().Be(TimeSpan.FromHours(20));
        targeted.GapHours.Should().Be(TimeSpan.FromHours(20));

        var bestEffort = result.Value.Single(d => d.DemandId == f.BestEffortDemandId);
        bestEffort.IsBestEffort.Should().BeTrue();
        bestEffort.GapHours.Should().BeNull();       // no target ⇒ no defined gap (§7)
        bestEffort.CoveredHours.Should().Be(TimeSpan.FromHours(10)); // 25% × 8h × 5d
    }

    [Fact]
    public async Task SingleDemand_Coverage()
    {
        var f = Seed();

        var result = await f.Svc.GetDemandCoverageForDemandAsync(f.TargetedDemandId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.GapHours.Should().Be(TimeSpan.FromHours(20));
        result.Value.RoleName.Should().Be("Dev");
    }

    [Fact]
    public async Task RoleMismatchCoverage_StillCountsTowardTheDemand()
    {
        // The best-effort Phase demand asks for a Designer; a Dev covers it. The
        // covered hours still count — surface, don't enforce (§6).
        var f = Seed();

        var result = await f.Svc.GetDemandCoverageForDemandAsync(f.BestEffortDemandId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleName.Should().Be("Designer");           // what was asked
        result.Value.CoveredHours.Should().Be(TimeSpan.FromHours(10)); // a Dev's coverage counts
    }
}
