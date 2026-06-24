using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Skills;
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
    public Guid RoleSkillId { get; private set; }

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
    // a second resource and a skill. Returns the harness ready to run commands.
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
        var skill = Skill.Create("Backend");

        db.Resources.AddRange(resource, other);
        db.ProjectNodes.Add(node);
        db.Skills.Add(skill);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        h.ResourceId = resource.Id;
        h.OtherResourceId = other.Id;
        h.ProjectNodeId = node.Id;
        h.RoleSkillId = skill.Id;
        return h;
    }

    // Persists an assigned allocation directly (bypassing the command pipeline),
    // for arrange steps. Returns the new id.
    public Guid SeedAllocation(DateOnly start, DateOnly end, decimal percent = 50m)
    {
        var a = Allocation.Create(ResourceId, ProjectNodeId, start, end, percent);
        Db.Allocations.Add(a);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return a.Id;
    }

    public int AllocationCount() => Db.Allocations.AsNoTracking().Count();

    public Allocation Reload(Guid id) =>
        Db.Allocations.AsNoTracking().Single(a => a.Id == id);
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
