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

    // "Today" for everything rolling-from-today (the frozen zone of ADR-0036).
    public static readonly DateOnly Today = new(2026, 6, 22);

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
            new Repository<CommitmentPolicyConfiguration, Guid>(db), db);
        var capacity = new FixedCapacity(capacityPerDay);
        // The real consequence service over the same InMemory db, on a pinned
        // clock: the frozen zone is rolling-from-today, and a test that reads
        // the wall clock passes until the day it does not.
        var consequences = new PlanConsequenceService(
            db,
            capacity,
            new LoadBandConfigurationService(new Repository<LoadBandConfiguration, Guid>(db), db),
            new TimeFenceConfigurationService(new Repository<TimeFenceConfiguration, Guid>(db), db),
            new FixedClock(Today));
        Service = new PlanCommandService(db, capacity, CommitmentPolicy, consequences);
    }

    // Seeds an active resource, a root Project node (Draft, given commitment),
    // a second resource and a role (the catalogue a Demand names as required,
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

    // ── Boundary helpers (ADR-0035) ─────────────────────────────────────────

    // Persists a Phase under the given root with the given planned window and
    // returns its id. A phase is the natural home of a coverage, and therefore
    // its referent; null dates make that edge impossible to follow.
    public Guid SeedPhase(Guid rootId, DateOnly? plannedStart, DateOnly? plannedEnd, string name = "Fase")
    {
        var root = Db.ProjectNodes.AsNoTracking().Single(p => p.Id == rootId);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, name, null);
        if (plannedStart is not null || plannedEnd is not null) phase.Replan(plannedStart, plannedEnd);
        Db.ProjectNodes.Add(phase);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return phase.Id;
    }

    // Sets a node's planned window directly (arrange). The default root is
    // seeded UNPLANNED on purpose: a coverage created with no dates on it must
    // fail, and that is a case worth having in the suite.
    public void PlanNode(Guid nodeId, DateOnly? start, DateOnly? end)
    {
        var n = Db.ProjectNodes.Single(p => p.Id == nodeId);
        n.Replan(start, end);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    // Persists a coverage on `demandId` whose edges FOLLOW the node, spanning
    // exactly the node's planned window (arrange for propagation tests).
    public Guid SeedFollowingCoverage(Guid demandId, decimal percent = 50m, Guid? resourceId = null)
    {
        var nodeId = Db.Demands.AsNoTracking().Where(d => d.Id == demandId).Select(d => d.ProjectNodeId).Single();
        var node = Db.ProjectNodes.AsNoTracking().Single(p => p.Id == nodeId);
        var from = node.PlannedStart!.Value;
        var to = node.PlannedEnd!.Value;

        var a = Allocation.CreateCoverage(demandId, nodeId, resourceId ?? ResourceId, from, to, percent);
        a.FollowNode(BoundaryEdge.Start, from);
        a.FollowNode(BoundaryEdge.End, to);
        Db.Allocations.Add(a);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return a.Id;
    }

    // Declares the seeded resource's availability boundary directly (arrange).
    // Under ADR-0035 this is decoration, never propagation — nothing follows it.
    public void SetResourceAvailability(Guid resourceId, DateOnly? from, DateOnly? until)
    {
        var r = Db.Resources.Single(x => x.Id == resourceId);
        r.SetAvailability(from, until);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    // Persists an imposed date on a root and returns its id. Since ADR-0035 a
    // constraint is a registry object and the origin of the negotiation delta —
    // never a referent, so nothing here can be followed.
    public Guid SeedConstraint(Guid rootId, DateOnly date, string name = "Go-live",
        ConstraintAuthority authority = ConstraintAuthority.Contractual)
    {
        var c = ExternalConstraint.Create(rootId, name, date, authority);
        Db.ExternalConstraints.Add(c);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return c.Id;
    }

    // Persists a second root project (for retarget arrange).
    public Guid SeedRoot(string name = "Proj2", CommitmentLevel commitment = CommitmentLevel.Committed)
    {
        var node = ProjectNode.CreateRoot(name, name, ProjectType.Internal, commitment, leadResourceId: null);
        Db.ProjectNodes.Add(node);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return node.Id;
    }

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

// Working capacity that can be TAKEN AWAY mid-test: sweep once so signals exist,
// then flip Refuses and assert the next pass fails rather than resolving them.
internal sealed class SwitchableCapacity(TimeSpan perDay) : ICapacityQueryService
{
    private readonly FixedCapacity working = new(perDay);
    private readonly RefusingCapacity refusing = new();

    public bool Refuses { get; set; }

    private ICapacityQueryService Current => Refuses ? refusing : working;

    public Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Current.GetForResourceAsync(resourceId, from, toInclusive, ct);

    public Task<ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>> GetForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Current.GetForResourcesAsync(resourceIds, from, toInclusive, ct);

    public Task<ServiceResult<IReadOnlyList<ResourceCapacityDto>>> GetSegmentsForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Current.GetSegmentsForResourcesAsync(resourceIds, from, toInclusive, ct);
}

// A capacity service that refuses every read (the shape a range wider than the cap
// produces). Reconciliation must surface the refusal: treating it as zero capacity
// turns a rejected request into "everything is uncovered" with an HTTP 200.
internal sealed class RefusingCapacity : ICapacityQueryService
{
    private static ServiceError Refused => ServiceError.Validation(new Dictionary<string, string[]>
    {
        ["range"] = ["Refused by the fake."]
    });

    public Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Task.FromResult(ServiceResult<IReadOnlyList<DailyCapacityDto>>.Failure(Refused));

    public Task<ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>> GetForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Task.FromResult(ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Failure(Refused));

    public Task<ServiceResult<IReadOnlyList<ResourceCapacityDto>>> GetSegmentsForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        Task.FromResult(ServiceResult<IReadOnlyList<ResourceCapacityDto>>.Failure(Refused));
}

internal sealed class FixedCapacity(TimeSpan perDay) : ICapacityQueryService
{
    public Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        return Task.FromResult(ServiceResult<IReadOnlyList<DailyCapacityDto>>.Success(Days(from, toInclusive)));
    }

    // Batch twin: same fixed series per requested id. The fake has no notion of
    // "all active resources", so null/empty ids yields an empty dictionary.
    public Task<ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>> GetForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        var result = (resourceIds ?? []).Distinct().ToDictionary(id => id, _ => Days(from, toInclusive));
        return Task.FromResult(ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Success(result));
    }

    public async Task<ServiceResult<IReadOnlyList<ResourceCapacityDto>>> GetSegmentsForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        var daily = await GetForResourcesAsync(resourceIds, from, toInclusive, ct);
        var dtos = daily.Value
            .Select(kvp => new ResourceCapacityDto { ResourceId = kvp.Key, Segments = CapacitySegments.Compress(kvp.Value) })
            .OrderBy(x => x.ResourceId)
            .ToList();
        return ServiceResult<IReadOnlyList<ResourceCapacityDto>>.Success(dtos);
    }

    private IReadOnlyList<DailyCapacityDto> Days(DateOnly from, DateOnly toInclusive)
    {
        var list = new List<DailyCapacityDto>();
        for (var d = from; d <= toInclusive; d = d.AddDays(1))
            list.Add(new DailyCapacityDto { Date = d, Hours = perDay });
        return list;
    }
}

internal sealed class FixedClock(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
