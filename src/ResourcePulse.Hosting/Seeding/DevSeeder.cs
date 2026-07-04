using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Domain.Teams;
using ResourcePulse.Persistence;

namespace ResourcePulse.Hosting.Seeding;

// Dev-only seeder: idempotent by-name checks ensure a re-run leaves the DB
// stable. Seeds a default Mon–Fri 9–18 calendar (only if none exists), then
// skills, tags, roles, 2 teams, 10 resources (5 per team), 5 projects with
// varied commitment/status, and a spread of allocations (assigned + placeholder,
// Tentative + Hard, overlapping + cross-project) so the plan-command API
// (POST /api/plan/commands) has realistic data to exercise out of the box.
// One resource is linked to the FakeAuth dev sub so skill-approval works.
public static class DevSeeder
{
    private const string DevUserSub = "dev-user-001";

    private static readonly string[] SkillNames =
    [
        "C#", "TypeScript", "React", "PostgreSQL", "Docker",
        "Kubernetes", "UX Design", "Project Management", "DevOps", "Testing"
    ];

    private static readonly string[] TagNames =
    [
        "senior", "junior", "remote", "onsite", "frontend",
        "backend", "fullstack", "mobile", "lead", "contractor"
    ];

    private static readonly string[] RoleNames =
    [
        "Sviluppatore", "Designer", "Project Manager", "QA Engineer",
        "Data Analyst", "DevOps Engineer", "Team Lead", "Product Owner"
    ];

    private const string TeamAlpha = "Team Alpha";
    private const string TeamBeta = "Team Beta";

    // name, team, roleName, primary skill (all best-effort wiring for test data).
    private static readonly (string Name, string Team, string Role, string Skill)[] People =
    [
        ("Mario Rossi",    TeamAlpha, "Team Lead",        "C#"),
        ("Luigi Bianchi",  TeamAlpha, "Sviluppatore",     "C#"),
        ("Giulia Verdi",   TeamAlpha, "Sviluppatore",     "TypeScript"),
        ("Marco Neri",     TeamAlpha, "QA Engineer",      "Testing"),
        ("Sara Gialli",    TeamAlpha, "Designer",         "UX Design"),
        ("Anna Blu",       TeamBeta,  "Project Manager",  "Project Management"),
        ("Paolo Viola",    TeamBeta,  "Sviluppatore",     "React"),
        ("Elena Rosa",     TeamBeta,  "DevOps Engineer",  "DevOps"),
        ("Davide Grigi",   TeamBeta,  "Data Analyst",     "PostgreSQL"),
        ("Chiara Arancio", TeamBeta,  "Sviluppatore",     "Docker"),
    ];

    // name, code, type, commitment, start? (Active vs Draft)
    private static readonly (string Name, string Code, ProjectType Type, CommitmentLevel Commitment, bool Start)[] Projects =
    [
        ("Apollo",          "APL", ProjectType.Customer,    CommitmentLevel.Committed,   true),
        ("Borealis",        "BOR", ProjectType.Internal,    CommitmentLevel.Planned,     false),
        ("Cosmos",          "CSM", ProjectType.Investment,  CommitmentLevel.Critical,    true),
        ("Delta Migration", "DLT", ProjectType.Maintenance, CommitmentLevel.Committed,   false),
        ("Echo R&D",        "ECO", ProjectType.Internal,    CommitmentLevel.Exploratory, false),
    ];

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // AuditInterceptor reads ICurrentUserAccessor.Sub at SavingChanges
        // time; the HTTP-backed accessor returns empty outside a request. Pin
        // a fake HttpContext for the duration of seeding so audit fields get
        // populated as "seeder".
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "seeder"),
                new Claim(ClaimTypes.Name, "Dev Seeder")
            ], "Seed"))
        };

        try
        {
            var db = sp.GetRequiredService<ResourcePulseDbContext>();

            var calendarId = await EnsureDefaultCalendarAsync(db);
            await EnsureSkillsAsync(db);
            await EnsureTagsAsync(db);
            await EnsureRolesAsync(db);
            await EnsureTeamsAsync(db);
            await EnsureResourcesAsync(db, calendarId);
            await EnsureProjectsAsync(db);
            await EnsureAllocationsAsync(db);

            logger.LogInformation("Dev seeding complete (calendar={CalendarId}).", calendarId);
        }
        finally
        {
            httpContextAccessor.HttpContext = null;
        }
    }

    private static async Task<Guid> EnsureDefaultCalendarAsync(ResourcePulseDbContext db)
    {
        var existing = await db.BusinessCalendars
            .Where(c => c.IsDefault)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        if (existing is not null) return existing.Value;

        var calendar = BusinessCalendar.Create("Default Calendar", isDefault: true);
        var validFrom = new DateOnly(2025, 1, 1);
        foreach (var day in new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        })
        {
            calendar.AddWorkWindow(WorkWindow.Create(
                day,
                new TimeOnly(9, 0),
                new TimeOnly(18, 0),
                validFrom,
                validTo: null));
        }

        db.BusinessCalendars.Add(calendar);
        await db.SaveChangesAsync();
        return calendar.Id;
    }

    private static async Task EnsureSkillsAsync(ResourcePulseDbContext db)
    {
        var existing = await db.Skills.Select(s => s.Name).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = SkillNames
            .Where(n => !existingSet.Contains(n))
            .Select(n => Skill.Create(n))
            .ToList();

        if (toAdd.Count == 0) return;
        db.Skills.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureTagsAsync(ResourcePulseDbContext db)
    {
        var existing = await db.Tags.Select(t => t.Name).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = TagNames
            .Where(n => !existingSet.Contains(n))
            .Select(n => Tag.Create(n))
            .ToList();

        if (toAdd.Count == 0) return;
        db.Tags.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureRolesAsync(ResourcePulseDbContext db)
    {
        var existing = await db.Roles.Select(r => r.Name).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = RoleNames
            .Where(n => !existingSet.Contains(n))
            .Select(n => Role.Create(n))
            .ToList();

        if (toAdd.Count == 0) return;
        db.Roles.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureTeamsAsync(ResourcePulseDbContext db)
    {
        var existing = await db.Teams.Select(t => t.Name).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = new[] { TeamAlpha, TeamBeta }
            .Where(n => !existingSet.Contains(n))
            .Select(Team.Create)
            .ToList();

        if (toAdd.Count == 0) return;
        db.Teams.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureResourcesAsync(ResourcePulseDbContext db, Guid calendarId)
    {
        var teamByName = await db.Teams.ToDictionaryAsync(t => t.Name, t => t.Id);
        var roleByName = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        var skillByName = await db.Skills.ToDictionaryAsync(s => s.Name, s => s.Id);

        foreach (var (name, team, roleName, skillName) in People)
        {
            var resource = await db.Resources.FirstOrDefaultAsync(r => r.Name == name);
            var isNew = resource is null;
            resource ??= Resource.Create(name, calendarId);

            // Idempotent wiring: assign team/role only when missing, so a re-run
            // upgrades previously-seeded bare resources without churn.
            if (resource.TeamId is null && teamByName.TryGetValue(team, out var teamId))
                resource.AssignToTeam(teamId);
            if (resource.RoleId is null && roleByName.TryGetValue(roleName, out var roleId))
                resource.AssignToRole(roleId);

            if (name == "Mario Rossi") resource.LinkToUser(DevUserSub);

            if (isNew)
            {
                if (skillByName.TryGetValue(skillName, out var skillId))
                    resource.AddSkill(skillId, SkillLevel.Proficient);
                db.Resources.Add(resource);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureProjectsAsync(ResourcePulseDbContext db)
    {
        var existing = await db.ProjectNodes
            .Where(p => p.ParentId == null)
            .Select(p => p.Name)
            .ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, code, type, commitment, start) in Projects)
        {
            if (existingSet.Contains(name)) continue;

            var root = ProjectNode.CreateRoot(name, code, type, commitment, leadResourceId: null);
            if (start) root.Start(); // Draft -> Active
            db.ProjectNodes.Add(root);
        }

        await db.SaveChangesAsync();
    }

    // Allocations have no natural key; only seed when the table is empty so a
    // re-run never duplicates them.
    private static async Task EnsureAllocationsAsync(ResourcePulseDbContext db)
    {
        if (await db.Allocations.AnyAsync()) return;

        var resources = await db.Resources.ToDictionaryAsync(r => r.Name, r => r.Id);
        var resourceRole = await db.Resources
            .Where(r => r.RoleId != null)
            .ToDictionaryAsync(r => r.Name, r => r.RoleId!.Value);
        var projects = await db.ProjectNodes
            .Where(p => p.ParentId == null)
            .ToDictionaryAsync(p => p.Name, p => p.Id);
        var anyRoleId = await db.Roles.Select(r => (Guid?)r.Id).FirstOrDefaultAsync();

        var demands = new List<Demand>();
        var toAdd = new List<Allocation>();

        // Coverage model (Phase 5.1, ADR-0025): every assignment covers a demand.
        // A demand carries the role; here we seed it from the covered person's role
        // (Declared, best-effort target). Deallocation would leave the demand.
        void Assign(string person, string project, DateOnly start, DateOnly end, decimal percent,
            AllocationStatus status = AllocationStatus.Tentative)
        {
            if (!resources.TryGetValue(person, out var rid) || !projects.TryGetValue(project, out var pid))
                return;
            var roleId = resourceRole.TryGetValue(person, out var rr) ? rr : anyRoleId;
            if (roleId is null) return;

            var demand = Demand.Create(pid, roleId.Value, requiredHours: null, DemandProvenance.Declared);
            demands.Add(demand);
            toAdd.Add(Allocation.CreateCoverage(demand.Id, pid, rid, start, end, percent, notes: null, status: status));
        }

        // Uncovered demand (the old "placeholder"): a targeted Demand with NO
        // coverage on it — the native "to be staffed" state (revision §8).
        void OpenDemand(string project, TimeSpan requiredHours,
            DemandProvenance provenance = DemandProvenance.Declared)
        {
            if (anyRoleId is Guid roleId && projects.TryGetValue(project, out var pid))
                demands.Add(Demand.Create(pid, roleId, requiredHours, provenance));
        }

        var jul = new DateOnly(2026, 7, 1);
        var aug = new DateOnly(2026, 8, 1);
        var sep = new DateOnly(2026, 9, 1);
        DateOnly End(DateOnly from, int days) => from.AddDays(days);

        // Apollo (Committed, Active): a full team, some Hard, an overlapping top-up.
        Assign("Mario Rossi",   "Apollo", jul, End(jul, 60), 50m, AllocationStatus.Hard);
        Assign("Luigi Bianchi", "Apollo", jul, End(jul, 45), 80m, AllocationStatus.Hard);
        Assign("Giulia Verdi",  "Apollo", aug, End(aug, 30), 60m);
        // Overlapping top-up on the same (resource, project) — rate% sums (ADR-0014).
        Assign("Luigi Bianchi", "Apollo", End(jul, 20), End(jul, 35), 30m);

        // Borealis (Planned, Draft): tentative only.
        Assign("Anna Blu",     "Borealis", jul, End(jul, 90), 25m);
        Assign("Paolo Viola",  "Borealis", aug, End(aug, 40), 50m);

        // Cosmos (Critical, Active): heavy commitment + a cross-project overload.
        Assign("Elena Rosa",   "Cosmos", jul, End(jul, 75), 70m, AllocationStatus.Hard);
        Assign("Davide Grigi", "Cosmos", aug, End(aug, 50), 100m, AllocationStatus.Hard);
        // Giulia is also on Apollo in Aug — cross-project overallocation (permitted, I5).
        Assign("Giulia Verdi", "Cosmos", aug, End(aug, 20), 60m);

        // Delta Migration (Committed, Draft): a mix + an uncovered demand.
        Assign("Chiara Arancio", "Delta Migration", sep, End(sep, 30), 40m);
        OpenDemand("Delta Migration", TimeSpan.FromHours(240));

        // Echo R&D (Exploratory, Draft): a coverage + an uncovered demand.
        Assign("Sara Gialli", "Echo R&D", jul, End(jul, 25), 30m);
        OpenDemand("Echo R&D", TimeSpan.FromHours(160));

        if (demands.Count == 0) return;
        db.Demands.AddRange(demands);
        db.Allocations.AddRange(toAdd);
        await db.SaveChangesAsync();
    }
}
