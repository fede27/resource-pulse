using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Resource commitment profile read-model (gap #4+#10 / ADR-0023): the service
// resolves each allocation's root project, decomposes the segment percent by
// project, and resolves project names. Exercises the EF query layer over InMemory.
public class ResourceCommitmentProfileTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Wed = new(2026, 6, 3);
    private static readonly DateOnly Thu = new(2026, 6, 4);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private sealed class Fixture
    {
        public LiveLoadQueryService Svc { get; init; } = null!;
        public Guid ResourceId { get; init; }
        public Guid AlphaRootId { get; init; }
        public Guid BetaRootId { get; init; }
    }

    // Resource staffed on Alpha (root) all week @ 50% and on a Phase of Beta
    // Wed-Thu @ 30%. A second resource on Alpha must not leak in.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"profile-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var calendar = Guid.NewGuid();
        var r = Resource.Create("Tizio", calendar);
        var other = Resource.Create("Caio", calendar);
        var role = Role.Create("Backend");

        var alpha = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, CommitmentLevel.Committed, null);
        var beta = ProjectNode.CreateRoot("Beta", "B", ProjectType.Internal, CommitmentLevel.Committed, null);
        var betaPhase = ProjectNode.CreateChild(beta, ProjectNodeType.Phase, "Beta Phase 1", null);

        var dAlpha = Demand.Create(alpha.Id, role.Id, null, DemandProvenance.Declared);
        var dBetaPhase = Demand.Create(betaPhase.Id, role.Id, null, DemandProvenance.Declared);

        var onAlpha = Allocation.CreateCoverage(dAlpha.Id, alpha.Id, r.Id, Mon, Fri, 50m);
        var onBetaPhase = Allocation.CreateCoverage(dBetaPhase.Id, betaPhase.Id, r.Id, Wed, Thu, 30m);
        var otherOnAlpha = Allocation.CreateCoverage(dAlpha.Id, alpha.Id, other.Id, Mon, Fri, 90m);

        db.Resources.AddRange(r, other);
        db.Roles.Add(role);
        db.ProjectNodes.AddRange(alpha, beta, betaPhase);
        db.Demands.AddRange(dAlpha, dBetaPhase);
        db.Allocations.AddRange(onAlpha, onBetaPhase, otherOnAlpha);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture
        {
            Svc = new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            ResourceId = r.Id,
            AlphaRootId = alpha.Id,
            BetaRootId = beta.Id
        };
    }

    [Fact]
    public async Task Profile_SegmentsAtBoundaries_WithPerProjectBreakdown()
    {
        var f = Seed();

        var result = await f.Svc.GetCommitmentProfileForResourceAsync(f.ResourceId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        var segments = result.Value;
        segments.Should().HaveCount(3);

        // Mon-Tue: Alpha only @ 50.
        segments[0].To.Should().Be(Mon.AddDays(1));
        segments[0].Percent.Should().Be(50m);
        segments[0].ByProject.Should().ContainSingle();

        // Wed-Thu: Alpha 50 + Beta 30 = 80; Beta resolved to its ROOT (name "Beta").
        segments[1].From.Should().Be(Wed);
        segments[1].To.Should().Be(Thu);
        segments[1].Percent.Should().Be(80m);
        segments[1].ByProject.Should().HaveCount(2);
        // Ordered by percent desc → Alpha first.
        segments[1].ByProject[0].ProjectNodeId.Should().Be(f.AlphaRootId);
        segments[1].ByProject[0].ProjectName.Should().Be("Alpha");
        segments[1].ByProject[0].Percent.Should().Be(50m);
        segments[1].ByProject[1].ProjectNodeId.Should().Be(f.BetaRootId);
        segments[1].ByProject[1].ProjectName.Should().Be("Beta"); // root name, not the phase
        segments[1].ByProject[1].Percent.Should().Be(30m);

        // Fri: back to Alpha only.
        segments[2].From.Should().Be(Fri);
        segments[2].Percent.Should().Be(50m);
    }

    [Fact]
    public async Task Profile_PeakIsMaxSegmentPercent()
    {
        var f = Seed();

        var result = await f.Svc.GetCommitmentProfileForResourceAsync(f.ResourceId, Mon, Fri);

        // The peak is a trivial caller-side derivation, not a bespoke field.
        result.Value.Max(s => s.Percent).Should().Be(80m);
    }

    [Fact]
    public async Task Profile_StatusFilter_NarrowsToRequestedStatus()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"profile-{Guid.NewGuid()}")
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

        var svc = new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8)));

        var hardOnly = await svc.GetCommitmentProfileForResourceAsync(r.Id, Mon, Fri, AllocationStatus.Hard);
        hardOnly.IsSuccess.Should().BeTrue();
        hardOnly.Value.Max(s => s.Percent).Should().Be(50m); // tentative 30% excluded

        var all = await svc.GetCommitmentProfileForResourceAsync(r.Id, Mon, Fri);
        all.Value.Max(s => s.Percent).Should().Be(80m); // default = every block
    }

    [Fact]
    public async Task Profile_NonexistentResource_NotFound()
    {
        var f = Seed();

        var result = await f.Svc.GetCommitmentProfileForResourceAsync(Guid.NewGuid(), Mon, Fri);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task Profile_FromAfterTo_Validation()
    {
        var f = Seed();

        var result = await f.Svc.GetCommitmentProfileForResourceAsync(f.ResourceId, Fri, Mon);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
    }
}
