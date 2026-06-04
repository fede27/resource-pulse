using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Persistence;

namespace ResourcePulse.Hosting.Seeding;

// Dev-only seeder: idempotent by-name checks ensure a re-run leaves the DB
// stable. Seeds a default Mon–Fri 9–18 calendar (only if none exists), then
// 10 skills, 10 tags, and 2 resources. One resource is linked to the
// FakeAuth dev sub so the skill-approval endpoints work out of the box.
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
            await EnsureResourcesAsync(db, calendarId);

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

    private static async Task EnsureResourcesAsync(ResourcePulseDbContext db, Guid calendarId)
    {
        await EnsureResourceAsync(db, "Mario Rossi", calendarId, DevUserSub);
        await EnsureResourceAsync(db, "Luigi Bianchi", calendarId, userSub: null);
    }

    private static async Task EnsureResourceAsync(
        ResourcePulseDbContext db,
        string name,
        Guid calendarId,
        string? userSub)
    {
        var exists = await db.Resources.AnyAsync(r => r.Name == name);
        if (exists) return;

        var resource = Resource.Create(name, calendarId);
        if (userSub is not null) resource.LinkToUser(userSub);

        db.Resources.Add(resource);
        await db.SaveChangesAsync();
    }
}
