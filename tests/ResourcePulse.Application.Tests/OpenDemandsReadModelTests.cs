using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Open-demands read model (ADR-0027): cross-project "what is still uncovered for
// this role" — the query behind the Allocazioni drag-to-cover picker. Open =
// GapHours > 0 or best-effort; fully-covered and surplus demands drop out; roots
// Closed/Cancelled are excluded (I4 would forbid covering them).
public class OpenDemandsReadModelTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5); // 5 days × 8h = 40h capacity

    private sealed class Fixture
    {
        public LiveLoadQueryService Svc { get; init; } = null!;
        public Guid DevRoleId { get; init; }
        public Guid DesignerRoleId { get; init; }
        public Guid OpenDevDemandId { get; init; }
        public Guid BestEffortDesignerDemandId { get; init; }
        public Guid CoveredDevDemandId { get; init; }
        public Guid ClosedRootDemandId { get; init; }
        public Guid AlphaRootId { get; init; }
    }

    // Two projects. Alpha (Active): a Dev demand 40h covered 20h (open, gap 20h),
    // a Dev demand 20h covered 20h (fully covered ⇒ not open), a best-effort
    // Designer demand on its Phase (open by definition). Omega (Closed): a Dev
    // demand fully uncovered — excluded despite the residual.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"opendemands-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var cal = Guid.NewGuid();
        var dev = Role.Create("Dev");
        var designer = Role.Create("Designer");
        var tizio = Resource.Create("Tizio", cal);
        tizio.AssignToRole(dev.Id);

        var alpha = ProjectNode.CreateRoot("Alpha", "A1", ProjectType.Internal, CommitmentLevel.Committed, null);
        var phase = ProjectNode.CreateChild(alpha, ProjectNodeType.Phase, "Phase 1", null);
        var omega = ProjectNode.CreateRoot("Omega", "O1", ProjectType.Internal, CommitmentLevel.Committed, null);
        omega.Start();
        omega.Complete(); // Closed root — its demands must not surface

        var openDev = Demand.Create(alpha.Id, dev.Id, TimeSpan.FromHours(40), DemandProvenance.Declared);
        var coveredDev = Demand.Create(alpha.Id, dev.Id, TimeSpan.FromHours(20), DemandProvenance.Declared);
        var bestEffort = Demand.Create(phase.Id, designer.Id, null, DemandProvenance.Declared);
        var closedRootDemand = Demand.Create(omega.Id, dev.Id, TimeSpan.FromHours(30), DemandProvenance.Declared);

        // openDev: 50% × 8h × 5d = 20h of 40h ⇒ gap 20h. coveredDev: 50% ⇒ 20h of
        // 20h ⇒ gap 0 ⇒ closed. bestEffort and closedRootDemand: uncovered.
        var c1 = Allocation.CreateCoverage(openDev.Id, alpha.Id, tizio.Id, Mon, Fri, 50m);
        var c2 = Allocation.CreateCoverage(coveredDev.Id, alpha.Id, tizio.Id, Mon, Fri, 50m);

        db.Roles.AddRange(dev, designer);
        db.Resources.Add(tizio);
        db.ProjectNodes.AddRange(alpha, phase, omega);
        db.Demands.AddRange(openDev, coveredDev, bestEffort, closedRootDemand);
        db.Allocations.AddRange(c1, c2);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture
        {
            Svc = new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            DevRoleId = dev.Id,
            DesignerRoleId = designer.Id,
            OpenDevDemandId = openDev.Id,
            BestEffortDesignerDemandId = bestEffort.Id,
            CoveredDevDemandId = coveredDev.Id,
            ClosedRootDemandId = closedRootDemand.Id,
            AlphaRootId = alpha.Id
        };
    }

    [Fact]
    public async Task AllRoles_ReturnsResidualAndBestEffort_ExcludesCoveredAndClosedRoots()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(null, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(d => d.DemandId).Should()
            .BeEquivalentTo([f.OpenDevDemandId, f.BestEffortDesignerDemandId]);
    }

    [Fact]
    public async Task RoleFilter_NarrowsToDemandsAskingForThatRole()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(f.DevRoleId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        var only = result.Value.Should().ContainSingle().Subject;
        only.DemandId.Should().Be(f.OpenDevDemandId);
        only.RoleName.Should().Be("Dev");
        only.RequiredHours.Should().Be(TimeSpan.FromHours(40));
        only.CoveredHours.Should().Be(TimeSpan.FromHours(20));
        only.GapHours.Should().Be(TimeSpan.FromHours(20));
    }

    [Fact]
    public async Task RootProjectIsResolved_AlsoForDemandsOnDescendantNodes()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(f.DesignerRoleId, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        var only = result.Value.Should().ContainSingle().Subject;
        only.DemandId.Should().Be(f.BestEffortDesignerDemandId);
        only.RootProjectId.Should().Be(f.AlphaRootId);   // Phase demand → Alpha root
        only.RootProjectName.Should().Be("Alpha");
        only.IsBestEffort.Should().BeTrue();
        only.GapHours.Should().BeNull();                 // no target ⇒ no defined gap (§7)
    }

    [Fact]
    public async Task ConcreteResidualsSortBeforeBestEffort()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(null, Mon, Fri);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].DemandId.Should().Be(f.OpenDevDemandId);
        result.Value[^1].IsBestEffort.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownRole_IsNotFound()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(Guid.NewGuid(), Mon, Fri);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ResourcePulse.Common.Results.ServiceErrorKind.NotFound);
    }

    [Fact]
    public async Task InvertedRange_IsValidationError()
    {
        var f = Seed();

        var result = await f.Svc.GetOpenDemandsAsync(null, Fri, Mon);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ResourcePulse.Common.Results.ServiceErrorKind.Validation);
    }
}
