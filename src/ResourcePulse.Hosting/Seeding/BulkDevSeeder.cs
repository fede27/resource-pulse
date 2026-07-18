using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Teams;
using ResourcePulse.Persistence;

namespace ResourcePulse.Hosting.Seeding;

// Dev-only BULK seeder: near-production volume on top of DevSeeder's baseline.
// Simulates ~2 years of project activity for a ~70-person Italian company:
//   - 60 additional resources (8 teams total, part-timers, a couple inactive)
//   - Italian public holidays + company shutdowns (2024-2026)
//   - 60 root projects (~30/year; previous year ~80% Closed) with phases,
//     baselines, actuals, and a mix of short/medium/long durations (max 9 months)
//   - ~350 demands + ~500 coverages (Hard/Tentative per I6, splits, overlaps,
//     cross-project overallocation, open demands to staff)
//
// All dates are anchored to "today" at run time (history = today-24 months,
// pipeline = today+3 months) so the dataset stays fresh whenever it is seeded.
// Generation is deterministic (fixed Random seed) for a given run date.
//
// Idempotency is all-or-nothing: bulk projects carry codes "P{yy}-{nn}"; if any
// root with such a code exists, the whole bulk pass is skipped. Runs AFTER
// DevSeeder — the 10 baseline people and 5 baseline projects are absorbed into
// the larger dataset (existing references never break).
public static class BulkDevSeeder
{
    private const int RandomSeed = 20260718;

    private static readonly string[] NewTeamNames =
    [
        "Delivery Milano", "Delivery Roma", "Platform Engineering",
        "Data & Analytics", "Design Studio", "PMO"
    ];

    private static readonly string[] NewRoleNames = ["Business Analyst", "Solution Architect"];

    private static readonly string[] NewSkillNames =
    [
        "Java", "Python", "Angular", "Azure", "Node.js",
        "Business Analysis", "Machine Learning", "Sicurezza Informatica", "SQL", "Flutter"
    ];

    private static readonly string[] FirstNames =
    [
        "Alessandro", "Andrea", "Marta", "Francesca", "Giovanni", "Lorenzo", "Matteo", "Martina",
        "Federica", "Simone", "Davide", "Elisa", "Alice", "Riccardo", "Stefano", "Laura",
        "Valentina", "Tommaso", "Giorgio", "Beatrice", "Camilla", "Nicola", "Fabio", "Silvia",
        "Roberta", "Pietro", "Emanuele", "Serena", "Ilaria", "Daniele", "Michele", "Veronica",
        "Claudia", "Gabriele", "Enrico", "Sofia", "Cristina", "Massimo", "Irene", "Filippo"
    ];

    private static readonly string[] LastNames =
    [
        "Ferrari", "Esposito", "Ricci", "Colombo", "Romano", "Gallo", "Costa", "Fontana",
        "Conti", "Marino", "Greco", "Bruno", "De Luca", "Moretti", "Barbieri", "Lombardi",
        "Giordano", "Rinaldi", "Mancini", "Villa", "Serra", "Leone", "Longo", "Martini",
        "Ferraro", "Santoro", "Caruso", "Pellegrini", "Fabbri", "Sartori", "Bellini", "Basile",
        "Riva", "Palmieri", "Farina", "Grassi", "Testa", "Pagano", "Battaglia", "Parisi"
    ];

    // Role mix for the 60 new people (weights sum to 100).
    private static readonly (string Role, int Weight)[] RoleMix =
    [
        ("Sviluppatore", 40), ("QA Engineer", 10), ("Designer", 7), ("Project Manager", 10),
        ("Team Lead", 7), ("DevOps Engineer", 7), ("Data Analyst", 6), ("Product Owner", 4),
        ("Business Analyst", 5), ("Solution Architect", 4)
    ];

    private static readonly string[] ProjectActions =
    [
        "Rinnovo", "Migrazione", "Evoluzione", "Rollout", "Integrazione",
        "Sviluppo", "Restyling", "Consolidamento", "Automazione", "Adeguamento"
    ];

    private static readonly string[] ProjectObjects =
    [
        "Portale Clienti", "ERP", "CRM", "App Mobile", "Data Platform",
        "E-commerce", "Intranet", "Sistema HR", "Fatturazione Elettronica", "Reportistica BI",
        "API Gateway", "Firma Digitale", "Gestione Magazzino", "Customer Care", "Sito Corporate"
    ];

    private static readonly string[] Clients =
    [
        "Alfa Retail S.p.A.", "Banca Popolare del Nord", "Gruppo Meridiana", "TecnoService S.r.l.",
        "Farmitalia", "Logistica Adriatica", "Energia Verde S.p.A.", "Assicura Group",
        "Editoriale Domani", "Metalmeccanica Fossati", "Villa dei Cedri Sanità",
        "Trasporti Bergamaschi", "Moda Milano Group", "AgriFood Piemonte", "Comune di Vicenza",
        "Università di Ferrara", "Porto di Trieste", "Caffè Borgni"
    ];

    private static readonly string[] CancelReasons =
    [
        "Budget non approvato", "Il cliente ha sospeso l'iniziativa", "Cambio di priorità di portafoglio"
    ];

    private static readonly string[] SuspendReasons =
    [
        "In attesa di conferma budget", "Cliente in riorganizzazione", "Dipendenza da fornitore esterno"
    ];

    private static readonly string[] OpenDemandNotes =
    [
        "Da staffare", "Profilo senior richiesto", "In attesa di conferma budget", "Possibile proroga"
    ];

    // Easter Monday (Lunedì dell'Angelo) — precomputed; skipped for unlisted years.
    private static readonly Dictionary<int, DateOnly> EasterMondays = new()
    {
        [2024] = new(2024, 4, 1), [2025] = new(2025, 4, 21), [2026] = new(2026, 4, 6),
        [2027] = new(2027, 3, 29), [2028] = new(2028, 4, 17)
    };

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Same audit trick as DevSeeder: pin a fake HttpContext so the
        // AuditInterceptor stamps rows as "bulk-seeder".
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "bulk-seeder"),
                new Claim(ClaimTypes.Name, "Bulk Dev Seeder")
            ], "Seed"))
        };

        try
        {
            var db = sp.GetRequiredService<ResourcePulseDbContext>();

            // All-or-nothing marker: bulk root projects use codes "P{yy}-{nn}".
            var alreadySeeded = await db.ProjectNodes
                .AnyAsync(p => p.ParentId == null && p.Code != null && EF.Functions.Like(p.Code, "P2_-%"));
            if (alreadySeeded)
            {
                logger.LogInformation("Bulk seeding skipped (marker project codes already present).");
                return;
            }

            var rng = new Random(RandomSeed);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var historyStart = new DateOnly(today.Year - 2, today.Month, 1);

            var calendarId = await EnsureCalendarCoversHistoryAsync(db, historyStart);
            await EnsureClosuresAsync(db, historyStart, today);
            var roles = await EnsureCatalogAsync(db, db.Roles, r => r.Name, r => r.Id, NewRoleNames, n => Role.Create(n));
            var skills = await EnsureCatalogAsync(db, db.Skills, s => s.Name, s => s.Id, NewSkillNames, n => Skill.Create(n));
            var teams = await EnsureCatalogAsync(db, db.Teams, t => t.Name, t => t.Id, NewTeamNames, n => Team.Create(n));

            var people = await EnsurePeopleAsync(db, rng, calendarId, historyStart, teams, roles, skills);
            var projects = await EnsureProjectsAsync(db, rng, today, historyStart, people, roles);
            await EnsureDemandsAndCoveragesAsync(db, rng, today, people, projects, roles);

            logger.LogInformation(
                "Bulk seeding complete: {People} people, {Projects} projects.",
                people.Count, projects.Count);
        }
        finally
        {
            httpContextAccessor.HttpContext = null;
        }
    }

    // ── Calendar & closures ─────────────────────────────────────────────────

    // DevSeeder's default calendar starts its windows at 2025-01-01; our history
    // starts earlier. Backfill each weekday's pattern over [historyStart, its
    // current ValidFrom) so historical capacity is non-zero.
    private static async Task<Guid> EnsureCalendarCoversHistoryAsync(ResourcePulseDbContext db, DateOnly historyStart)
    {
        var calendar = await db.BusinessCalendars.FirstAsync(c => c.IsDefault);

        var backfilled = new List<WorkWindow>();
        foreach (var group in calendar.WorkWindows.GroupBy(w => w.DayOfWeek))
        {
            var earliest = group.MinBy(w => w.ValidFrom)!;
            if (earliest.ValidFrom > historyStart)
            {
                var window = WorkWindow.Create(
                    earliest.DayOfWeek, earliest.StartTime, earliest.EndTime,
                    historyStart, earliest.ValidFrom);
                calendar.AddWorkWindow(window);
                backfilled.Add(window);
            }
        }

        if (backfilled.Count > 0)
        {
            // The calendar is tracked and the owned windows carry factory-assigned
            // keys, so DetectChanges attaches them as pre-existing (Modified) and
            // SaveChanges would issue a 0-row UPDATE. Force them to Added.
            db.ChangeTracker.DetectChanges();
            var collection = db.Entry(calendar).Collection(c => c.WorkWindows);
            foreach (var window in backfilled)
                collection.FindEntry(window)!.State = EntityState.Added;
            await db.SaveChangesAsync();
        }

        return calendar.Id;
    }

    private static async Task EnsureClosuresAsync(ResourcePulseDbContext db, DateOnly historyStart, DateOnly today)
    {
        var existingFroms = (await db.CompanyClosures.Select(c => c.DateFrom).ToListAsync()).ToHashSet();
        var toAdd = new List<CompanyClosure>();

        void Add(DateOnly from, DateOnly to, string reason)
        {
            if (!existingFroms.Contains(from))
                toAdd.Add(CompanyClosure.Create(from, to, reason));
        }

        for (var year = historyStart.Year; year <= today.Year + 1; year++)
        {
            Add(new(year, 1, 1), new(year, 1, 1), "Capodanno");
            Add(new(year, 1, 6), new(year, 1, 6), "Epifania");
            if (EasterMondays.TryGetValue(year, out var pasquetta))
                Add(pasquetta, pasquetta, "Lunedì dell'Angelo");
            Add(new(year, 4, 25), new(year, 4, 25), "Festa della Liberazione");
            Add(new(year, 5, 1), new(year, 5, 1), "Festa del Lavoro");
            Add(new(year, 6, 2), new(year, 6, 2), "Festa della Repubblica");
            Add(new(year, 11, 1), new(year, 11, 1), "Ognissanti");
            Add(new(year, 12, 8), new(year, 12, 8), "Immacolata");
            Add(new(year, 12, 25), new(year, 12, 26), "Natale e Santo Stefano");

            // Company shutdowns: Ferragosto week + end-of-year bridge.
            var aug15 = new DateOnly(year, 8, 15);
            var ferragostoMonday = aug15.AddDays(-(((int)aug15.DayOfWeek + 6) % 7));
            Add(ferragostoMonday, ferragostoMonday.AddDays(4), "Chiusura estiva");
            Add(new(year, 12, 27), new(year, 12, 31), "Chiusura di fine anno");
        }

        if (toAdd.Count > 0)
        {
            db.CompanyClosures.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    // ── Catalogs (roles / skills / teams) ───────────────────────────────────

    // Idempotent by-name catalog top-up; returns the FULL name→id map (existing + new).
    private static async Task<Dictionary<string, Guid>> EnsureCatalogAsync<T>(
        ResourcePulseDbContext db,
        DbSet<T> set,
        Func<T, string> nameOf,
        Func<T, Guid> idOf,
        string[] wanted,
        Func<string, T> create) where T : class
    {
        var all = await set.ToListAsync();
        var names = all.Select(nameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = wanted.Where(n => !names.Contains(n)).Select(create).ToList();
        if (toAdd.Count > 0)
        {
            set.AddRange(toAdd);
            await db.SaveChangesAsync();
            all.AddRange(toAdd);
        }

        return all.ToDictionary(nameOf, idOf, StringComparer.OrdinalIgnoreCase);
    }

    // ── People ──────────────────────────────────────────────────────────────

    private sealed record Person(Resource Resource, Guid? RoleId, bool IsActive);

    private static async Task<List<Person>> EnsurePeopleAsync(
        ResourcePulseDbContext db,
        Random rng,
        Guid calendarId,
        DateOnly historyStart,
        Dictionary<string, Guid> teams,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> skills)
    {
        const int targetHeadcount = 70;

        var existing = await db.Resources.ToListAsync();
        var usedNames = existing.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skillIds = skills.Values.ToArray();
        var newTeamIds = NewTeamNames.Select(n => teams[n]).ToArray();

        var toAdd = new List<Resource>();
        var inactivePicks = new HashSet<int> { 21, 43 }; // deterministic turnover
        var i = 0;
        while (existing.Count + toAdd.Count < targetHeadcount)
        {
            var name = $"{Pick(rng, FirstNames)} {Pick(rng, LastNames)}";
            if (!usedNames.Add(name)) continue;

            var resource = Resource.Create(name, calendarId);
            resource.AssignToTeam(newTeamIds[rng.Next(newTeamIds.Length)]);
            resource.AssignToRole(roles[PickWeighted(rng, RoleMix)]);
            resource.SetEmail(EmailFor(name));

            foreach (var skillId in PickDistinct(rng, skillIds, rng.Next(2, 5)))
                resource.AddSkill(skillId, PickSkillLevel(rng));

            // ~10% part-time: personal Mon-Fri 9-13 pattern replaces the calendar's.
            if (i % 10 == 7)
            {
                foreach (var day in new[]
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday
                })
                {
                    resource.AddWorkWindowOverride(WorkWindow.Create(
                        day, new TimeOnly(9, 0), new TimeOnly(13, 0), historyStart, validTo: null));
                }
            }

            if (inactivePicks.Contains(i)) resource.Deactivate();

            toAdd.Add(resource);
            i++;
        }

        if (toAdd.Count > 0)
        {
            db.Resources.AddRange(toAdd);
            await db.SaveChangesAsync();
        }

        return existing.Concat(toAdd)
            .Select(r => new Person(r, r.RoleId, r.IsActive))
            .ToList();
    }

    private static string EmailFor(string fullName) =>
        string.Join('.', fullName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        + "@pulse-demo.it";

    // ── Projects ────────────────────────────────────────────────────────────

    private sealed record SeededProject(
        ProjectNode Root,
        List<ProjectNode> Phases,
        DateOnly PlannedStart,
        DateOnly PlannedEnd,
        ProjectStatus Status,
        bool HardCommitted);

    private enum Fate { Closed, Cancelled, ActiveLate, Active, OnHold, Pipeline }

    private static async Task<List<SeededProject>> EnsureProjectsAsync(
        ResourcePulseDbContext db,
        Random rng,
        DateOnly today,
        DateOnly historyStart,
        List<Person> people,
        Dictionary<string, Guid> roles)
    {
        // Lead pool: PM-ish roles (falls back to anyone if empty).
        var pmRoleIds = new[] { "Project Manager", "Team Lead", "Product Owner" }
            .Where(roles.ContainsKey).Select(n => roles[n]).ToHashSet();
        var leadPool = people.Where(p => p.IsActive && p.RoleId is { } r && pmRoleIds.Contains(r)).ToList();
        if (leadPool.Count == 0) leadPool = people.Where(p => p.IsActive).ToList();

        // Unique names from the action × object catalog.
        var nameCombos = ProjectActions
            .SelectMany(a => ProjectObjects.Select(o => $"{a} {o}"))
            .ToList();
        Shuffle(rng, nameCombos);

        // Fates: year 1 = 30 (80% archived), year 2 = 30 (mixed, incl. pipeline).
        var fates = new List<Fate>();
        fates.AddRange(Enumerable.Repeat(Fate.Closed, 24));     // year 1
        fates.AddRange(Enumerable.Repeat(Fate.Cancelled, 2));
        fates.AddRange(Enumerable.Repeat(Fate.ActiveLate, 4));
        fates.AddRange(Enumerable.Repeat(Fate.Closed, 6));      // year 2
        fates.Add(Fate.Cancelled);
        fates.AddRange(Enumerable.Repeat(Fate.Active, 15));
        fates.AddRange(Enumerable.Repeat(Fate.OnHold, 3));
        fates.AddRange(Enumerable.Repeat(Fate.Pipeline, 5));

        var result = new List<SeededProject>();
        var codeCounters = new Dictionary<int, int>();

        for (var i = 0; i < fates.Count; i++)
        {
            var fate = fates[i];
            var isYear1 = i < 30;
            var duration = RollDurationDays(rng);
            if (fate == Fate.ActiveLate && duration < 180) duration = rng.Next(180, 271);

            var plannedStart = fate switch
            {
                // Started long ago, planned end 1-8 months in the past, still open.
                Fate.ActiveLate => today.AddDays(-duration - rng.Next(30, 240)),
                // Must span today: 20-70% elapsed.
                Fate.Active or Fate.OnHold => today.AddDays(-(int)(duration * (0.2 + rng.NextDouble() * 0.5))),
                Fate.Pipeline => today.AddDays(rng.Next(14, 90)),
                _ when isYear1 => historyStart.AddDays(rng.Next(0, 330)),
                // Year-2 closed/cancelled: finished (or killed) safely in the past.
                _ => today.AddDays(-duration - rng.Next(30, 120))
            };
            plannedStart = AlignToMonday(plannedStart);
            var plannedEnd = plannedStart.AddDays(duration);

            var type = PickWeighted(rng, [
                (ProjectType.Customer, 55), (ProjectType.Internal, 20),
                (ProjectType.Maintenance, 15), (ProjectType.Investment, 10)
            ]);
            var commitment = fate == Fate.Pipeline
                ? PickWeighted(rng, [
                    (CommitmentLevel.Planned, 50), (CommitmentLevel.Exploratory, 30), (CommitmentLevel.Committed, 20)])
                : PickWeighted(rng, [
                    (CommitmentLevel.Committed, 60), (CommitmentLevel.Critical, 15),
                    (CommitmentLevel.Planned, 20), (CommitmentLevel.Exploratory, 5)]);

            var client = type is ProjectType.Customer or ProjectType.Maintenance ? Pick(rng, Clients) : null;
            var lead = Pick(rng, leadPool);
            var code = NextCode(codeCounters, plannedStart.Year);

            var root = ProjectNode.CreateRoot(nameCombos[i], code, type, commitment, lead.Resource.Id, client);
            root.Replan(plannedStart, plannedEnd);
            if (rng.NextDouble() < 0.85) root.Baseline(plannedStart, plannedEnd);

            // Closed projects slip a little; replan when the slip is material.
            var slipDays = fate == Fate.Closed ? rng.Next(-10, 36) : 0;
            if (slipDays > 14)
            {
                plannedEnd = plannedEnd.AddDays(slipDays);
                root.Replan(plannedStart, plannedEnd);
            }

            ApplyLifecycle(rng, root, fate, today, plannedStart, plannedEnd, slipDays);

            // ~60% of projects >= ~2.5 months get 2-4 sequential phases.
            var phases = new List<ProjectNode>();
            if (duration >= 75 && rng.NextDouble() < 0.6)
            {
                var phaseNames = new[] { "Analisi", "Sviluppo", "Test", "Rilascio" };
                var phaseCount = rng.Next(2, 5);
                var cursor = plannedStart;
                for (var p = 0; p < phaseCount; p++)
                {
                    var isLast = p == phaseCount - 1;
                    var chunkEnd = isLast
                        ? plannedEnd
                        : cursor.AddDays(duration / phaseCount + rng.Next(-7, 8));
                    if (chunkEnd <= cursor) chunkEnd = cursor.AddDays(7);
                    if (chunkEnd > plannedEnd) chunkEnd = plannedEnd;

                    var phase = ProjectNode.CreateChild(root, ProjectNodeType.Phase, phaseNames[p], $"{code}-F{p + 1}");
                    phase.Replan(cursor, chunkEnd);
                    phases.Add(phase);
                    cursor = chunkEnd.AddDays(1);
                    if (cursor > plannedEnd) break;
                }
            }

            db.ProjectNodes.Add(root);
            db.ProjectNodes.AddRange(phases);

            var hardCommitted = commitment is CommitmentLevel.Committed or CommitmentLevel.Critical;
            result.Add(new SeededProject(root, phases, plannedStart, plannedEnd, root.Status!.Value, hardCommitted));
        }

        await db.SaveChangesAsync();
        return result;
    }

    // Start()/Complete() stamp actuals with the real "now"; BackfillActuals then
    // rewrites them with the simulated history (it rejects future dates, hence
    // the clamps).
    private static void ApplyLifecycle(
        Random rng, ProjectNode root, Fate fate, DateOnly today,
        DateOnly plannedStart, DateOnly plannedEnd, int slipDays)
    {
        var actualStart = Min(plannedStart.AddDays(rng.Next(-3, 8)), today);

        switch (fate)
        {
            case Fate.Closed:
                var actualEnd = Min(plannedEnd.AddDays(slipDays > 14 ? 0 : slipDays), today.AddDays(-7));
                if (actualEnd < actualStart) actualEnd = actualStart;
                root.Start();
                root.Complete();
                root.BackfillActuals(actualStart, actualEnd);
                break;

            case Fate.Cancelled:
                var cancelDate = Min(plannedStart.AddDays((int)((plannedEnd.DayNumber - plannedStart.DayNumber) *
                    (0.3 + rng.NextDouble() * 0.3))), today.AddDays(-14));
                if (cancelDate < actualStart) cancelDate = actualStart;
                root.Start();
                root.Cancel(Pick(rng, CancelReasons));
                root.BackfillActuals(actualStart, cancelDate);
                break;

            case Fate.ActiveLate:
            case Fate.Active:
                root.Start();
                root.BackfillActuals(actualStart, null);
                break;

            case Fate.OnHold:
                root.Start();
                root.Suspend(Pick(rng, SuspendReasons));
                root.BackfillActuals(actualStart, null);
                break;

            case Fate.Pipeline:
                break; // stays Draft, no actuals
        }
    }

    // ── Demands & coverages ─────────────────────────────────────────────────

    private static async Task EnsureDemandsAndCoveragesAsync(
        ResourcePulseDbContext db,
        Random rng,
        DateOnly today,
        List<Person> people,
        List<SeededProject> projects,
        Dictionary<string, Guid> roles)
    {
        var activePeople = people.Where(p => p.IsActive).ToList();
        var roleIds = roles.Values.ToArray();
        var fallbackRoleId = roleIds[0];
        var pmRoleIds = new[] { "Project Manager", "Team Lead", "Product Owner" }
            .Where(roles.ContainsKey).Select(n => roles[n]).ToHashSet();

        var demands = new List<Demand>();
        var allocations = new List<Allocation>();

        foreach (var project in projects)
        {
            var span = project.PlannedEnd.DayNumber - project.PlannedStart.DayNumber;
            var isCurrent = project.Status is ProjectStatus.Active or ProjectStatus.OnHold or ProjectStatus.Draft;

            // Inactive people may appear only on archived projects.
            var pool = isCurrent ? activePeople : people;
            var teamSize = span switch { < 80 => rng.Next(2, 5), < 160 => rng.Next(3, 7), _ => rng.Next(5, 9) };
            var members = PickDistinct(rng, pool, Math.Min(teamSize, pool.Count));

            foreach (var member in members)
            {
                var roleId = member.RoleId ?? fallbackRoleId;
                var isPm = pmRoleIds.Contains(roleId);
                var percent = isPm
                    ? Pick(rng, [20m, 25m, 30m, 40m])
                    : Pick(rng, [30m, 40m, 50m, 50m, 60m, 60m, 70m, 80m, 80m, 100m]);

                // Member window: starts within the first fifth, covers 50-100% of the rest.
                var offset = rng.Next(0, Math.Max(1, span / 5));
                var start = project.PlannedStart.AddDays(offset);
                var length = Math.Max(10, (int)((span - offset) * (0.5 + rng.NextDouble() * 0.5)));
                var end = Min(start.AddDays(length), project.PlannedEnd);

                // Demand target: ~70% declared in hours (workdays × 8h × rate), rest best-effort.
                var node = PickNode(rng, project);
                TimeSpan? required = rng.NextDouble() < 0.7
                    ? TimeSpan.FromHours(Math.Max(8, RoundTo8((end.DayNumber - start.DayNumber + 1) * 5.0 / 7.0 * 8 * (double)percent / 100)))
                    : null;
                var provenance = rng.NextDouble() < 0.8 ? DemandProvenance.Declared : DemandProvenance.Inferred;
                var demand = Demand.Create(node.Id, roleId, required, provenance);
                demands.Add(demand);

                // I6: Hard only on hard-committed roots (and never on pipeline Drafts).
                var status = project.HardCommitted && project.Status != ProjectStatus.Draft && rng.NextDouble() < 0.75
                    ? AllocationStatus.Hard
                    : AllocationStatus.Tentative;

                // ~15% of long-enough engagements get a mid-way rate change (two adjacent blocks).
                if (end.DayNumber - start.DayNumber >= 40 && rng.NextDouble() < 0.15)
                {
                    var mid = start.AddDays((end.DayNumber - start.DayNumber) / 2);
                    var secondRate = Math.Clamp(percent + Pick(rng, [-20m, 20m]), 10m, 100m);
                    allocations.Add(Allocation.CreateCoverage(
                        demand.Id, node.Id, member.Resource.Id, start, mid.AddDays(-1), percent, status: status));
                    allocations.Add(Allocation.CreateCoverage(
                        demand.Id, node.Id, member.Resource.Id, mid, end, secondRate, status: status));
                }
                else
                {
                    allocations.Add(Allocation.CreateCoverage(
                        demand.Id, node.Id, member.Resource.Id, start, end, percent, status: status));
                }

                // ~7%: overlapping top-up on the same lane — rate% sums (ADR-0014).
                if (end.DayNumber - start.DayNumber >= 30 && rng.NextDouble() < 0.07)
                {
                    var tuStart = start.AddDays((end.DayNumber - start.DayNumber) / 3);
                    var tuEnd = Min(tuStart.AddDays((end.DayNumber - start.DayNumber) / 3), end);
                    allocations.Add(Allocation.CreateCoverage(
                        demand.Id, node.Id, member.Resource.Id, tuStart, tuEnd,
                        Pick(rng, [20m, 25m, 30m]), notes: "Rinforzo temporaneo"));
                }
            }

            // Open (uncovered) demands on current projects — material for the
            // "copri capacità libera" picker.
            if (isCurrent)
            {
                var openCount = rng.NextDouble() < 0.6 ? (rng.NextDouble() < 0.25 ? 2 : 1) : 0;
                for (var d = 0; d < openCount; d++)
                {
                    var node = PickNode(rng, project);
                    demands.Add(Demand.Create(
                        node.Id,
                        roleIds[rng.Next(roleIds.Length)],
                        TimeSpan.FromHours(RoundTo8(rng.Next(80, 401))),
                        DemandProvenance.Declared,
                        ownerResourceId: rng.NextDouble() < 0.6 ? project.Root.LeadResourceId : null,
                        notes: Pick(rng, OpenDemandNotes)));
                }
            }
        }

        db.Demands.AddRange(demands);
        db.Allocations.AddRange(allocations);
        await db.SaveChangesAsync();
    }

    // Demand target node: root, or one of the phases half of the time (I1: both
    // Project and Phase are capacity-planning levels).
    private static ProjectNode PickNode(Random rng, SeededProject project) =>
        project.Phases.Count > 0 && rng.NextDouble() < 0.5
            ? project.Phases[rng.Next(project.Phases.Count)]
            : project.Root;

    // ── Small helpers ───────────────────────────────────────────────────────

    private static int RollDurationDays(Random rng) => rng.NextDouble() switch
    {
        < 0.4 => rng.Next(35, 75),    // short: 5-10 weeks
        < 0.8 => rng.Next(90, 155),   // medium: 3-5 months
        _ => rng.Next(180, 271)       // long: 6-9 months (hard max)
    };

    private static string NextCode(Dictionary<int, int> counters, int year)
    {
        counters.TryGetValue(year, out var n);
        counters[year] = ++n;
        return $"P{year % 100:00}-{n:00}";
    }

    private static DateOnly AlignToMonday(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        return date;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static double RoundTo8(double hours) => Math.Round(hours / 8) * 8;

    private static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    private static SkillLevel PickSkillLevel(Random rng) => rng.NextDouble() switch
    {
        < 0.25 => SkillLevel.Basic,
        < 0.80 => SkillLevel.Proficient,
        _ => SkillLevel.Expert
    };

    private static T PickWeighted<T>(Random rng, (T Value, int Weight)[] options)
    {
        var roll = rng.Next(options.Sum(o => o.Weight));
        foreach (var (value, weight) in options)
        {
            if (roll < weight) return value;
            roll -= weight;
        }
        return options[^1].Value;
    }

    private static List<T> PickDistinct<T>(Random rng, IReadOnlyList<T> items, int count)
    {
        var indexes = Enumerable.Range(0, items.Count).ToList();
        Shuffle(rng, indexes);
        return indexes.Take(Math.Min(count, items.Count)).Select(x => items[x]).ToList();
    }

    private static void Shuffle<T>(Random rng, IList<T> list)
    {
        for (var n = list.Count - 1; n > 0; n--)
        {
            var k = rng.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
