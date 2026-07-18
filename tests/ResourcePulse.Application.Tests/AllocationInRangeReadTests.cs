using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;

namespace ResourcePulse.Application.Tests;

// Flat plan-slice read (consolidation P3): every coverage overlapping the range
// in one call, with the root project resolved relationally on the DTO — also on
// coverage sitting on a Phase, where the root is not the node itself.
public class AllocationInRangeReadTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private sealed record Fixture(
        AllocationService Svc,
        Guid RootId,
        Guid PhaseId,
        Guid InRangeId,
        Guid OnPhaseId,
        Guid OutOfRangeId);

    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"allocrange-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var resource = Resource.Create("Tizio", Guid.NewGuid());
        var role = Role.Create("Backend");
        var root = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Fase 1", "A1");
        var demandRoot = Demand.Create(root.Id, role.Id, null, DemandProvenance.Declared);
        var demandPhase = Demand.Create(phase.Id, role.Id, null, DemandProvenance.Declared);

        var inRange = Allocation.CreateCoverage(demandRoot.Id, root.Id, resource.Id, Mon, Fri, 50m);
        var onPhase = Allocation.CreateCoverage(demandPhase.Id, phase.Id, resource.Id, Mon.AddDays(2), Fri.AddDays(10), 30m);
        var outOfRange = Allocation.CreateCoverage(demandRoot.Id, root.Id, resource.Id, Fri.AddDays(30), Fri.AddDays(40), 20m);

        db.Resources.Add(resource);
        db.Roles.Add(role);
        db.ProjectNodes.AddRange(root, phase);
        db.Demands.AddRange(demandRoot, demandPhase);
        db.Allocations.AddRange(inRange, onPhase, outOfRange);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture(
            new AllocationService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            root.Id, phase.Id, inRange.Id, onPhase.Id, outOfRange.Id);
    }

    [Fact]
    public async Task ReturnsOnlyCoverageOverlappingTheRange()
    {
        var f = Seed();

        var result = await f.Svc.GetInRangeAsync(Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.Id).Should().BeEquivalentTo([f.InRangeId, f.OnPhaseId]);
    }

    [Fact]
    public async Task ResolvesTheRootProject_AlsoForPhaseLevelCoverage()
    {
        var f = Seed();

        var result = await f.Svc.GetInRangeAsync(Mon, Fri);

        var onRoot = result.Value.Single(x => x.Id == f.InRangeId);
        onRoot.RootProjectId.Should().Be(f.RootId);
        onRoot.RootProjectName.Should().Be("Alpha");

        var onPhase = result.Value.Single(x => x.Id == f.OnPhaseId);
        onPhase.ProjectNodeId.Should().Be(f.PhaseId);
        onPhase.RootProjectId.Should().Be(f.RootId); // root ≠ node: resolved via Path
        onPhase.RootProjectName.Should().Be("Alpha");
    }

    [Fact]
    public async Task RootProject_IsResolvedOnTheExistingListReadsToo()
    {
        var f = Seed();

        var byResource = await f.Svc.GetForProjectNodeAsync(f.RootId, Mon, Fri);

        byResource.Value.Should().OnlyContain(x => x.RootProjectId == f.RootId && x.RootProjectName == "Alpha");
    }

    [Fact]
    public async Task InvalidOrOversizedRange_IsRejected()
    {
        var f = Seed();

        (await f.Svc.GetInRangeAsync(Fri, Mon)).IsFailure.Should().BeTrue();
        (await f.Svc.GetInRangeAsync(Mon, Mon.AddDays(400))).IsFailure.Should().BeTrue();
    }
}
