using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Application-layer integration harness for PlanCommandService. Uses the EF Core
// InMemory provider: the model carries Postgres-specific column types (citext,
// interval) and a HasPostgresExtension, so a relational SQLite EnsureCreated is
// brittle; InMemory is provider-agnostic and exercises exactly what these tests
// target — command dispatch, dryRun no-commit, cascade, overlap. (True DB-level
// constraint fidelity is a Testcontainers follow-up.)
internal sealed class PlanCommandHarness
{
    public ResourcePulseDbContext Db { get; }
    public PlanCommandService Service { get; }
    public ICommitmentPolicyService CommitmentPolicy { get; }

    public Guid CalendarId { get; } = Guid.NewGuid();
    public Guid ResourceId { get; private set; }
    public Guid OtherResourceId { get; private set; }
    public Guid ProjectNodeId { get; private set; }
    // Role catalogue id — the demand's required role.
    public Guid RoleId { get; private set; }
    // A default Declared demand on (ProjectNodeId, RoleId) for coverage arrange.
    public Guid DemandId { get; private set; }

    private PlanCommandHarness(ResourcePulseDbContext db, TimeSpan capacityPerDay)
    {
        Db = db;
        // Real CommitmentPolicyService over the same InMemory db so the I6 hard
        // gate reads the (get-or-seeded) threshold from config (ADR-0020).
        CommitmentPolicy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db));
        Service = new PlanCommandService(db, new FixedCapacity(capacityPerDay), CommitmentPolicy);
    }

    // Seeds an active resource, a root Project node (Draft, given commitment),
    // a second resource and a role (the open-role catalogue for placeholders,
    // ADR-0021 / M2). Returns the harness ready to run commands.
    public static PlanCommandHarness Create(
        CommitmentLevel commitment = CommitmentLevel.Committed,
        TimeSpan? capacityPerDay = null)
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"plan-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ResourcePulseDbContext(options);

        var h = new PlanCommandHarness(db, capacityPerDay ?? TimeSpan.FromHours(8));

        var resource = Resource.Create("Tizio", h.CalendarId);
        var other = Resource.Create("Caio", h.CalendarId);
        var node = ProjectNode.CreateRoot("Proj", "P1", ProjectType.Internal, commitment, leadResourceId: null);
        var role = Role.Create("Backend");
        var demand = Demand.Create(node.Id, role.Id, requiredHours: null, DemandProvenance.Declared);

        db.Resources.AddRange(resource, other);
        db.ProjectNodes.Add(node);
        db.Roles.Add(role);
        db.Demands.Add(demand);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        h.ResourceId = resource.Id;
        h.OtherResourceId = other.Id;
        h.ProjectNodeId = node.Id;
        h.RoleId = role.Id;
        h.DemandId = demand.Id;
        return h;
    }

    // Persists a coverage on the default demand (bypassing the command pipeline),
    // for arrange steps. Returns the new id.
    public Guid SeedAllocation(DateOnly start, DateOnly end, decimal percent = 50m) =>
        SeedCoverage(DemandId, start, end, percent);

    public Guid SeedCoverage(Guid demandId, DateOnly start, DateOnly end, decimal percent = 50m)
    {
        var nodeId = Db.Demands.AsNoTracking().Where(d => d.Id == demandId).Select(d => d.ProjectNodeId).Single();
        var a = Allocation.CreateCoverage(demandId, nodeId, ResourceId, start, end, percent);
        Db.Allocations.Add(a);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return a.Id;
    }

    public int AllocationCount() => Db.Allocations.AsNoTracking().Count();

    public Allocation Reload(Guid id) =>
        Db.Allocations.AsNoTracking().Single(a => a.Id == id);

    // ── Demand helpers (Phase 5.0) ───────────────────────────────────────────

    // Persists a fresh role and returns its id (for role-correction arrange).
    public Guid SeedRole(string name)
    {
        var role = Role.Create(name);
        Db.Roles.Add(role);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return role.Id;
    }

    // Seeds a demand on an explicit node (with the default role). For retarget
    // arrange, where the target demand lives on a different project.
    public Guid SeedDemandOn(Guid projectNodeId, TimeSpan? requiredHours = null)
    {
        var d = Demand.Create(projectNodeId, RoleId, requiredHours, DemandProvenance.Declared);
        Db.Demands.Add(d);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return d.Id;
    }

    public int DemandCount() => Db.Demands.AsNoTracking().Count();

    public Demand ReloadDemand(Guid id) =>
        Db.Demands.AsNoTracking().Single(d => d.Id == id);

    // Persists a demand directly (bypassing the command pipeline), for arrange
    // steps. Defaults to the seeded role on the seeded node.
    public Guid SeedDemand(
        TimeSpan? requiredHours = null,
        DemandProvenance provenance = DemandProvenance.Declared,
        Guid? ownerResourceId = null)
    {
        var d = Demand.Create(ProjectNodeId, RoleId, requiredHours, provenance, ownerResourceId);
        Db.Demands.Add(d);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return d.Id;
    }
}

internal sealed class FixedCapacity(TimeSpan perDay) : ICapacityQueryService
{
    public Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        var list = new List<DailyCapacityDto>();
        for (var d = from; d <= toInclusive; d = d.AddDays(1))
            list.Add(new DailyCapacityDto { Date = d, Hours = perDay });
        return Task.FromResult(ServiceResult<IReadOnlyList<DailyCapacityDto>>.Success(list));
    }
}
