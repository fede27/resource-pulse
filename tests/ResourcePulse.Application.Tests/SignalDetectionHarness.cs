using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Load;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.Application.Tests;

// Application harness for the triage detector (ADR-0032). Same InMemory posture
// as PlanCommandHarness, and for the same reason: the model carries Postgres
// column types, so what these tests target is reconciliation and detection logic,
// not DB-level constraint fidelity.
//
// Everything is wired REAL except capacity: the detector composes the same batch
// read models the boards use, and swapping them for fakes would test the fakes.
internal sealed class SignalDetectionHarness
{
    // A Monday, so week-shaped fixtures read plainly.
    public static readonly DateOnly Today = new(2026, 6, 1);

    public ResourcePulseDbContext Db { get; }
    public SignalDetectionService Detector { get; }
    public ISignalPolicyService Policy { get; }

    // Flip Refuses to take the capacity read away: what the detector does then is
    // the difference between "the plan holds" and "this pass could not look".
    public SwitchableCapacity Capacity { get; }

    // Controllable wall clock: the retention purge compares against it, and the
    // change feed's timestamps come from it.
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 6, 1, 6, 0, 0, TimeSpan.Zero));

    public Guid CalendarId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ResourceId { get; private set; }

    private SignalDetectionHarness(ResourcePulseDbContext db, TimeSpan capacityPerDay)
    {
        Db = db;
        var capacity = new SwitchableCapacity(capacityPerDay);
        Capacity = capacity;
        var load = new LiveLoadQueryService(db, capacity);

        Policy = new SignalPolicyService(new Repository<SignalPolicy, Guid>(db), db);

        Detector = new SignalDetectionService(
            db,
            load,
            new AllocationService(db, capacity),
            capacity,
            new TimeFenceConfigurationService(new Repository<TimeFenceConfiguration, Guid>(db), db),
            new LoadBandConfigurationService(new Repository<LoadBandConfiguration, Guid>(db), db),
            new CommitmentPolicyService(new Repository<CommitmentPolicyConfiguration, Guid>(db), db),
            Policy,
            new Repository<SignalSweepState, Guid>(db),
            Clock);
    }

    // Seeds a default business calendar, a role, one active resource and one
    // Active root project spanning today → +6 months (so its derived deadline
    // lands well inside the horizon).
    public static SignalDetectionHarness Create(
        CommitmentLevel commitment = CommitmentLevel.Committed,
        TimeSpan? capacityPerDay = null)
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"signals-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ResourcePulseDbContext(options);

        var h = new SignalDetectionHarness(db, capacityPerDay ?? TimeSpan.FromHours(8));

        var calendar = BusinessCalendar.Create("Standard", isDefault: true);
        var role = Role.Create("Backend");
        var resource = Resource.Create("Tizio", calendar.Id);
        var project = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, commitment, leadResourceId: null);
        project.Replan(Today.AddDays(30), Today.AddDays(180));
        project.Start();

        db.BusinessCalendars.Add(calendar);
        db.Roles.Add(role);
        db.Resources.Add(resource);
        db.ProjectNodes.Add(project);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        h.CalendarId = calendar.Id;
        h.RoleId = role.Id;
        h.ResourceId = resource.Id;
        h.ProjectId = project.Id;
        return h;
    }

    public Task<ResourcePulse.Common.Results.ServiceResult<SignalSweepResult>> SweepAsync(DateOnly? today = null) =>
        Detector.SweepAsync(today ?? Today);

    // ── Arrange helpers ──────────────────────────────────────────────────────

    public Guid SeedDemand(
        TimeSpan? requiredHours,
        Guid? nodeId = null,
        DateOnly? decideBy = null)
    {
        var demand = Demand.Create(
            nodeId ?? ProjectId, RoleId, requiredHours, DemandProvenance.Declared, decideBy: decideBy);
        Db.Demands.Add(demand);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return demand.Id;
    }

    public Guid SeedCoverage(
        Guid demandId,
        DateOnly from,
        DateOnly to,
        decimal percent = 50m,
        AllocationStatus status = AllocationStatus.Hard,
        Guid? resourceId = null)
    {
        var nodeId = Db.Demands.AsNoTracking().Where(d => d.Id == demandId).Select(d => d.ProjectNodeId).Single();
        var coverage = Allocation.CreateCoverage(demandId, nodeId, resourceId ?? ResourceId, from, to, percent);
        if (status == AllocationStatus.Hard) coverage.ChangeStatus(AllocationStatus.Hard);
        Db.Allocations.Add(coverage);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return coverage.Id;
    }

    public Guid SeedResource(string name, bool active = true)
    {
        var r = Resource.Create(name, CalendarId);
        if (!active) r.Deactivate();
        Db.Resources.Add(r);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return r.Id;
    }

    public Guid SeedProject(
        string name,
        CommitmentLevel commitment = CommitmentLevel.Committed,
        DateOnly? plannedStart = null,
        DateOnly? plannedEnd = null,
        bool cancelled = false)
    {
        var p = ProjectNode.CreateRoot(name, name[..1], ProjectType.Internal, commitment, leadResourceId: null);
        if (plannedStart is not null || plannedEnd is not null) p.Replan(plannedStart, plannedEnd);
        if (cancelled) p.Cancel("archiviato"); else p.Start();
        Db.ProjectNodes.Add(p);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return p.Id;
    }

    public Guid SeedPhase(string name, Guid rootId, DateOnly? plannedStart = null)
    {
        var root = Db.ProjectNodes.Single(n => n.Id == rootId);
        var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, name, null);
        if (plannedStart is not null) phase.Replan(plannedStart, plannedStart.Value.AddDays(30));
        Db.ProjectNodes.Add(phase);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return phase.Id;
    }

    // ── Assert helpers ───────────────────────────────────────────────────────

    public IReadOnlyList<PlanSignal> Live() =>
        Db.PlanSignals.AsNoTracking().Where(s => s.Detection == SignalDetection.Live).ToList();

    public PlanSignal? LiveOf(SignalKind kind, Guid? subjectId = null) =>
        Db.PlanSignals.AsNoTracking()
            .Where(s => s.Detection == SignalDetection.Live && s.Kind == kind)
            .ToList()
            .FirstOrDefault(s => subjectId is null || s.SubjectId == subjectId);

    public PlanSignal Reload(Guid id) =>
        Db.PlanSignals.AsNoTracking().Single(s => s.Id == id);

    public SignalSweepState SweepState() => Db.SignalSweepStates.AsNoTracking().Single();

    // ── Read/write surface (S3) ──────────────────────────────────────────────

    // The read model, as a given caller. `userSub` links to a Resource for the
    // "my projects" scope and identifies the visit marker.
    public SignalService ServiceAs(string userSub = "sub-elena") =>
        new(Db,
            new Repository<PlanSignal, Guid>(Db),
            new Repository<SignalVisit, Guid>(Db),
            new StubUser(userSub),
            Clock,
            new SweepCadence(48));

    // Links a resource to an auth subject, so "my projects" can resolve.
    public void LinkUser(Guid resourceId, string userSub)
    {
        var r = Db.Resources.Single(x => x.Id == resourceId);
        r.LinkToUser(userSub);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public void SetLead(Guid projectId, Guid resourceId)
    {
        var p = Db.ProjectNodes.Single(n => n.Id == projectId);
        p.AssignLead(resourceId);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    private sealed class StubUser(string sub) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public CurrentUser User { get; } = new(sub, $"{sub}@x", sub, new Dictionary<string, string>());
        public string? AuthenticationScheme => "FakeAuth";
    }
}

// Minimal controllable TimeProvider. Only GetUtcNow is exercised.
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
