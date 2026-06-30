using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Subtree aggregation (ADR-0022 / gap #5): both GET /api/allocations/by-project-node/{id}
// and GET /api/project-nodes/{id}/load must aggregate over root + descendants via
// the materialized-path prefix, not the exact node. Exercises the EF query layer
// over the InMemory provider (same rationale as PlanCommandHarness).
public class ProjectNodeSubtreeAggregationTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D5 = new(2026, 6, 5);

    private sealed class Fixture
    {
        public ResourcePulseDbContext Db { get; init; } = null!;
        public Guid RootId { get; init; }
        public Guid PhaseId { get; init; }
        public Guid R1 { get; init; }
        public Guid R2 { get; init; }
        public Guid RoleId { get; init; }
    }

    // Tree: Root(Project) → Phase → WorkPackage; plus an unrelated OtherRoot.
    // Allocations: R1@50% on Root, R2@100% on Phase, an open-role placeholder@40%
    // on Phase, and R1@100% on OtherRoot (must never leak into the subtree views).
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"subtree-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var calendar = Guid.NewGuid();
        var r1 = Resource.Create("Tizio", calendar);
        var r2 = Resource.Create("Caio", calendar);
        var role = Role.Create("Backend");

        var root = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, "Phase 1", null);
        var wp = ProjectNode.CreateChild(phase, ProjectNodeType.WorkPackage, "WP 1", null);
        var otherRoot = ProjectNode.CreateRoot("Other", "P2", ProjectType.Internal, CommitmentLevel.Committed, null);

        var aRoot = Allocation.Create(r1.Id, root.Id, D1, D5, 50m);
        var aPhase = Allocation.Create(r2.Id, phase.Id, D1, D5, 100m);
        var hole = Allocation.CreatePlaceholder(phase.Id, D1, D5, 40m, role.Id, ownerResourceId: null);
        var aOther = Allocation.Create(r1.Id, otherRoot.Id, D1, D5, 100m);

        db.Resources.AddRange(r1, r2);
        db.Roles.Add(role);
        db.ProjectNodes.AddRange(root, phase, wp, otherRoot);
        db.Allocations.AddRange(aRoot, aPhase, hole, aOther);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture
        {
            Db = db, RootId = root.Id, PhaseId = phase.Id,
            R1 = r1.Id, R2 = r2.Id, RoleId = role.Id
        };
    }

    // ── Allocations read (by-project-node) ──────────────────────────────────────

    [Fact]
    public async Task Allocations_ForRoot_IncludesRootAndPhaseBlocks_ExcludesOtherProject()
    {
        var f = Seed();
        var svc = new AllocationService(f.Db, new FixedCapacity(TimeSpan.FromHours(8)));

        var result = await svc.GetForProjectNodeAsync(f.RootId, D1, D5);

        result.IsSuccess.Should().BeTrue();
        // Root block (R1) + Phase block (R2) + Phase placeholder = 3; OtherRoot excluded.
        result.Value.Should().HaveCount(3);
        result.Value.Where(a => a.ResourceId == f.R1).Should().ContainSingle();
        result.Value.Where(a => a.ResourceId == f.R2).Should().ContainSingle();
        result.Value.Where(a => a.IsPlaceholder).Should().ContainSingle()
            .Which.RoleId.Should().Be(f.RoleId);
    }

    [Fact]
    public async Task Allocations_ForPhase_IncludesOnlyPhaseSubtree_NotRootBlock()
    {
        var f = Seed();
        var svc = new AllocationService(f.Db, new FixedCapacity(TimeSpan.FromHours(8)));

        var result = await svc.GetForProjectNodeAsync(f.PhaseId, D1, D5);

        result.IsSuccess.Should().BeTrue();
        // Phase subtree = Phase + WP (no blocks): the R2 block + the placeholder.
        result.Value.Should().HaveCount(2);
        result.Value.Should().NotContain(a => a.ResourceId == f.R1); // root block excluded
    }

    [Fact]
    public async Task Allocations_NonexistentNode_ReturnsEmpty()
    {
        var f = Seed();
        var svc = new AllocationService(f.Db, new FixedCapacity(TimeSpan.FromHours(8)));

        var result = await svc.GetForProjectNodeAsync(Guid.NewGuid(), D1, D5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Node load (subtree) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Load_ForRoot_AggregatesRootAndPhase_AndSurfacesPlaceholder()
    {
        var f = Seed();
        var svc = new LiveLoadQueryService(f.Db, new FixedCapacity(TimeSpan.FromHours(8)));

        var result = await svc.GetForProjectNodeAsync(f.RootId, D1, D5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
        result.Value.Should().AllSatisfy(d =>
        {
            // R1 50% (4h) + R2 100% (8h) = 12h; placeholder 40% in its own bucket.
            d.TotalHours.Should().Be(TimeSpan.FromHours(12));
            d.ByResource.Should().HaveCount(2);
            d.PlaceholderRatePercent.Should().Be(40m);
        });
    }

    [Fact]
    public async Task Load_ForPhase_ExcludesRootBlock()
    {
        var f = Seed();
        var svc = new LiveLoadQueryService(f.Db, new FixedCapacity(TimeSpan.FromHours(8)));

        var result = await svc.GetForProjectNodeAsync(f.PhaseId, D1, D5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(d =>
        {
            // Only R2 @ 100% (8h) on the Phase; the root R1 block is not in this subtree.
            d.TotalHours.Should().Be(TimeSpan.FromHours(8));
            d.ByResource.Should().ContainSingle().Which.ResourceId.Should().Be(f.R2);
            d.PlaceholderRatePercent.Should().Be(40m);
        });
    }
}
