using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Cross-project demand reconciliation (consolidation P4): every demand on a
// non-Closed/Cancelled root (I4), reconciled in one call, with the root project
// resolved on the DTO. Parity with the per-node subtree read is the key guard.
public class DemandCoverageInRangeTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5); // 5 days × 8h = 40h

    private sealed record Fixture(
        LiveLoadQueryService Svc,
        Guid AlphaId,
        Guid PhaseId,
        Guid RootDemandId,
        Guid PhaseDemandId,
        Guid CancelledDemandId);

    // Alpha (active): demand 40h on the root covered 50% by Tizio all week +
    // best-effort demand on a Phase. Omega (cancelled): one demand — excluded.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"demandrange-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var tizio = Resource.Create("Tizio", Guid.NewGuid());
        var role = Role.Create("Backend");
        var alpha = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(alpha, ProjectNodeType.Phase, "Fase 1", "A1");
        var omega = ProjectNode.CreateRoot("Omega", "O", ProjectType.Internal, CommitmentLevel.Committed, null);
        omega.Cancel("archiviato");

        var dRoot = Demand.Create(alpha.Id, role.Id, TimeSpan.FromHours(40), DemandProvenance.Declared);
        var dPhase = Demand.Create(phase.Id, role.Id, null, DemandProvenance.Inferred);
        var dOmega = Demand.Create(omega.Id, role.Id, TimeSpan.FromHours(10), DemandProvenance.Declared);

        var coverage = Allocation.CreateCoverage(dRoot.Id, alpha.Id, tizio.Id, Mon, Fri, 50m);

        db.Resources.Add(tizio);
        db.Roles.Add(role);
        db.ProjectNodes.AddRange(alpha, phase, omega);
        db.Demands.AddRange(dRoot, dPhase, dOmega);
        db.Allocations.Add(coverage);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture(
            new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            alpha.Id, phase.Id, dRoot.Id, dPhase.Id, dOmega.Id);
    }

    [Fact]
    public async Task ExcludesDemandsOnClosedOrCancelledRoots()
    {
        var f = Seed();

        var result = await f.Svc.GetDemandCoverageInRangeAsync(Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.DemandId).Should().BeEquivalentTo([f.RootDemandId, f.PhaseDemandId]);
    }

    [Fact]
    public async Task ResolvesTheRootProject_AlsoForPhaseLevelDemands()
    {
        var f = Seed();

        var result = await f.Svc.GetDemandCoverageInRangeAsync(Mon, Fri);

        var onPhase = result.Value.Single(x => x.DemandId == f.PhaseDemandId);
        onPhase.ProjectNodeId.Should().Be(f.PhaseId);
        onPhase.RootProjectId.Should().Be(f.AlphaId); // root ≠ node: via Path
        onPhase.RootProjectName.Should().Be("Alpha");
    }

    [Fact]
    public async Task ReconciliationMatchesThePerNodeSubtreeRead()
    {
        var f = Seed();

        var batch = await f.Svc.GetDemandCoverageInRangeAsync(Mon, Fri);
        var perNode = await f.Svc.GetDemandCoverageForProjectNodeAsync(f.AlphaId, Mon, Fri);

        var batchAlpha = batch.Value.Where(x => x.RootProjectId == f.AlphaId).ToList();
        batchAlpha.Should().BeEquivalentTo(perNode.Value);

        // Sanity on the numbers: 50% × 40h capacity = 20h covered, 20h gap.
        var rootRow = batchAlpha.Single(x => x.DemandId == f.RootDemandId);
        rootRow.CoveredHours.Should().Be(TimeSpan.FromHours(20));
        rootRow.GapHours.Should().Be(TimeSpan.FromHours(20));
        // Best-effort keeps its null gap (no fake zero).
        batchAlpha.Single(x => x.DemandId == f.PhaseDemandId).GapHours.Should().BeNull();
    }

    [Fact]
    public async Task InvalidOrOversizedRange_IsRejected()
    {
        var f = Seed();

        (await f.Svc.GetDemandCoverageInRangeAsync(Fri, Mon)).IsFailure.Should().BeTrue();
        (await f.Svc.GetDemandCoverageInRangeAsync(Mon, Mon.AddDays(400))).IsFailure.Should().BeTrue();
    }
}
