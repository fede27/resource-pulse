using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Signals;

// Read model + the two human gestures. Labels are batch-resolved per kind
// (ADR-0024 pattern): one query per label family, never one per row.
public sealed class SignalService(
    ResourcePulseDbContext db,
    IRepository<PlanSignal, Guid> signals,
    IRepository<SignalVisit, Guid> visits,
    ICurrentUserAccessor currentUser,
    TimeProvider clock,
    ISweepCadence cadence) : ISignalService
{
    public async Task<ServiceResult<IReadOnlyList<SignalDto>>> GetAsync(
        SignalScope scope, CancellationToken ct = default)
    {
        var live = await db.PlanSignals.AsNoTracking()
            .Where(s => s.Detection == SignalDetection.Live)
            .ToListAsync(ct);

        live = await ApplyScopeAsync(live, scope, ct);

        var labels = await ResolveLabelsAsync(live, ct);
        var lastVisit = await LastVisitedAtAsync(ct);

        // Ranked server-side, unlike the sustainability verdict which stayed on
        // the client: here the server holds the history that explains the order.
        var ranked = SignalRanking.Rank(live);

        return ServiceResult<IReadOnlyList<SignalDto>>.Success(
            ranked.Select(s => ToDto(s, labels, lastVisit)).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<SignalChangeDto>>> GetChangesAsync(
        SignalScope scope, DateTimeOffset? since, int max, CancellationToken ct = default)
    {
        var cutoff = since ?? await LastVisitedAtAsync(ct);
        if (cutoff is null)
            return ServiceResult<IReadOnlyList<SignalChangeDto>>.Success([]);

        // Both live and resolved: "Risolto" is a resolved row, and resolved rows
        // are exactly why the retention window is feed depth rather than cleanup.
        var changed = await db.PlanSignals.AsNoTracking()
            .Where(s => s.LastChangedAt != null && s.LastChangedAt > cutoff)
            .ToListAsync(ct);

        changed = await ApplyScopeAsync(changed, scope, ct);
        var labels = await ResolveLabelsAsync(changed, ct);

        var feed = changed
            .Where(s => s.LastChange != SignalChange.None)
            .OrderByDescending(s => s.LastChangedAt)
            .Take(max)
            .Select(s => ToChangeDto(s, labels))
            .ToList();

        return ServiceResult<IReadOnlyList<SignalChangeDto>>.Success(feed);
    }

    public async Task<ServiceResult<SignalSweepDto>> GetSweepAsync(CancellationToken ct = default)
    {
        var state = await db.SignalSweepStates.AsNoTracking().FirstOrDefaultAsync(ct);

        return ServiceResult<SignalSweepDto>.Success(new SignalSweepDto
        {
            // Null when the detector has never run here. The client MUST render
            // that as its own state: an empty queue is only "il piano tiene" if
            // we know we looked (ADR-0032 §10).
            LastSweptAt = state?.LastSweptAt,
            LiveCount = state?.LastLiveCount ?? 0,
            StaleAfterHours = cadence.StaleAfterHours,
            LastVisitedAt = await LastVisitedAtAsync(ct),
            QueueBudget = SignalRanking.QueueBudget
        });
    }

    public async Task<ServiceResult<SignalDto>> AcknowledgeAsync(
        Guid id, string? reason, CancellationToken ct = default)
    {
        var signal = await signals.GetByIdAsync(id, ct);
        if (signal is null) return ServiceResult<SignalDto>.NotFound($"Signal {id} not found.");

        try
        {
            signal.Acknowledge(Author(), reason, clock.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return ServiceResult<SignalDto>.Conflict(ex.Message);
        }

        await signals.SaveChangesAsync(ct);
        return await SingleAsync(signal, ct);
    }

    public async Task<ServiceResult<SignalDto>> ReopenAsync(Guid id, CancellationToken ct = default)
    {
        var signal = await signals.GetByIdAsync(id, ct);
        if (signal is null) return ServiceResult<SignalDto>.NotFound($"Signal {id} not found.");

        signal.Reopen(Author(), clock.GetUtcNow());
        await signals.SaveChangesAsync(ct);
        return await SingleAsync(signal, ct);
    }

    public async Task<ServiceResult<Unit>> RecordVisitAsync(CancellationToken ct = default)
    {
        var sub = currentUser.User.Sub;
        if (string.IsNullOrWhiteSpace(sub))
            return ServiceResult<Unit>.Conflict("An anonymous caller has no visit to record.");

        var now = clock.GetUtcNow();
        var visit = await db.SignalVisits.FirstOrDefaultAsync(v => v.UserSub == sub, ct);

        if (visit is null)
            await visits.AddAsync(SignalVisit.Create(sub, now), ct);
        else
            visit.Touch(now);   // monotonic: a stale request must not resurrect read rows

        await visits.SaveChangesAsync(ct);
        return ServiceResult<Unit>.Success(Unit.Value);
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    // "Mine" = the projects I LEAD, filtered through TouchedRootProjectIds — the
    // uniform rule of ADR-0032 §8. Without it, three kinds out of nine (the
    // person-subject one and the two aggregate families) would fall out of the
    // scope entirely and a PM would never see that the people on their own
    // project are overloaded.
    private async Task<List<PlanSignal>> ApplyScopeAsync(
        List<PlanSignal> source, SignalScope scope, CancellationToken ct)
    {
        if (scope == SignalScope.All) return source;

        var sub = currentUser.User.Sub;
        var resourceId = await db.Resources.AsNoTracking()
            .Where(r => r.UserSub == sub)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        // Not linked to a resource ⇒ leads nothing ⇒ "mine" is legitimately empty.
        if (resourceId is null) return [];

        var led = await db.ProjectNodes.AsNoTracking()
            .Where(n => n.ParentId == null && n.LeadResourceId == resourceId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var ledSet = led.ToHashSet();
        return source.Where(s => s.TouchedRootProjectIds.Any(ledSet.Contains)).ToList();
    }

    // ── Label resolution (ADR-0024: batch, never per row) ────────────────────

    private sealed record Labels(
        IReadOnlyDictionary<Guid, DemandLabel> Demands,
        IReadOnlyDictionary<Guid, AllocationLabel> Allocations,
        IReadOnlyDictionary<Guid, string> Resources,
        IReadOnlyDictionary<Guid, string> Projects);

    private sealed record DemandLabel(Guid ProjectNodeId, Guid RoleId, string RoleName, Guid RootProjectId, string? OwnerName);

    private sealed record AllocationLabel(Guid DemandId, Guid ResourceId, string ResourceName, string RoleName, Guid RootProjectId);

    private async Task<Labels> ResolveLabelsAsync(IReadOnlyList<PlanSignal> source, CancellationToken ct)
    {
        var demandIds = Subjects(source, SignalKind.Gap);
        var allocationIds = Subjects(source, SignalKind.TentativeInFrozen);
        var resourceIds = Subjects(source, SignalKind.Overcommit);

        // Aggregate members carry names too — the alphabetical, figure-free list
        // the UnderBand row shows.
        resourceIds.UnionWith(source
            .Where(s => s.Kind == SignalKind.UnderBand)
            .SelectMany(s => s.MemberSubjectIds));

        var projectIds = source.SelectMany(s => s.TouchedRootProjectIds).ToHashSet();

        var demands = await db.Demands.AsNoTracking()
            .Where(d => demandIds.Contains(d.Id))
            .Join(db.Roles, d => d.RoleId, r => r.Id, (d, r) => new { d.Id, d.ProjectNodeId, d.RoleId, RoleName = r.Name, d.OwnerResourceId })
            .Join(db.ProjectNodes, x => x.ProjectNodeId, n => n.Id, (x, n) => new { x.Id, x.ProjectNodeId, x.RoleId, x.RoleName, x.OwnerResourceId, n.Path })
            .ToListAsync(ct);

        var ownerIds = demands.Where(d => d.OwnerResourceId != null).Select(d => d.OwnerResourceId!.Value).ToHashSet();
        resourceIds.UnionWith(ownerIds);

        var allocations = await db.Allocations.AsNoTracking()
            .Where(a => allocationIds.Contains(a.Id))
            .Join(db.Demands, a => a.DemandId, d => d.Id, (a, d) => new { a.Id, a.DemandId, a.ResourceId, d.RoleId, a.ProjectNodeId })
            .Join(db.Roles, x => x.RoleId, r => r.Id, (x, r) => new { x.Id, x.DemandId, x.ResourceId, RoleName = r.Name, x.ProjectNodeId })
            .Join(db.ProjectNodes, x => x.ProjectNodeId, n => n.Id, (x, n) => new { x.Id, x.DemandId, x.ResourceId, x.RoleName, n.Path })
            .ToListAsync(ct);

        resourceIds.UnionWith(allocations.Select(a => a.ResourceId));

        var resourceNames = await db.Resources.AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        projectIds.UnionWith(demands.Select(d => ProjectNodePath.RootId(d.Path)));
        projectIds.UnionWith(allocations.Select(a => ProjectNodePath.RootId(a.Path)));

        var projectNames = await db.ProjectNodes.AsNoTracking()
            .Where(n => projectIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);

        return new Labels(
            demands.ToDictionary(d => d.Id, d => new DemandLabel(
                d.ProjectNodeId, d.RoleId, d.RoleName, ProjectNodePath.RootId(d.Path),
                d.OwnerResourceId is Guid o ? resourceNames.GetValueOrDefault(o) : null)),
            allocations.ToDictionary(a => a.Id, a => new AllocationLabel(
                a.DemandId, a.ResourceId, resourceNames.GetValueOrDefault(a.ResourceId, "—"),
                a.RoleName, ProjectNodePath.RootId(a.Path))),
            resourceNames,
            projectNames);
    }

    private static HashSet<Guid> Subjects(IReadOnlyList<PlanSignal> source, SignalKind kind) =>
        source.Where(s => s.Kind == kind && s.SubjectId is not null)
              .Select(s => s.SubjectId!.Value)
              .ToHashSet();

    // ── Projection ───────────────────────────────────────────────────────────

    // Kind-specific labels, resolved before the DTO is built. Read DTOs in this
    // codebase are classes with init-only members, not records, so the shape is
    // assembled once rather than patched with `with`.
    private sealed record KindParts(
        string? RoleName,
        string? ResourceName,
        string? RootProjectName,
        string? OwnerName,
        SignalLinkDto Link,
        IReadOnlyList<SignalContributionDto> Contributions)
    {
        public static readonly KindParts None = new(null, null, null, null, new SignalLinkDto(), []);
    }

    private static KindParts PartsFor(PlanSignal s, Labels labels) => s.Kind switch
    {
        SignalKind.Gap when s.SubjectId is Guid demandId && labels.Demands.TryGetValue(demandId, out var d) =>
            new KindParts(
                d.RoleName, null, labels.Projects.GetValueOrDefault(d.RootProjectId), d.OwnerName,
                new SignalLinkDto
                {
                    RootProjectId = d.RootProjectId,
                    ProjectNodeId = d.ProjectNodeId,
                    DemandId = demandId,
                    RoleId = d.RoleId
                },
                []),

        SignalKind.TentativeInFrozen when s.SubjectId is Guid allocId && labels.Allocations.TryGetValue(allocId, out var a) =>
            new KindParts(
                a.RoleName, a.ResourceName, labels.Projects.GetValueOrDefault(a.RootProjectId), null,
                new SignalLinkDto
                {
                    RootProjectId = a.RootProjectId,
                    DemandId = a.DemandId,
                    AllocationId = allocId,
                    ResourceId = a.ResourceId
                },
                []),

        SignalKind.Overcommit when s.SubjectId is Guid resourceId =>
            new KindParts(
                null, labels.Resources.GetValueOrDefault(resourceId), null, null,
                new SignalLinkDto { ResourceId = resourceId },
                // The decomposition the expander shows: WHAT MAKES UP the breach,
                // never a recommended move. Choosing who covers is a staffing
                // gesture and belongs on the page that shows the context.
                s.TouchedRootProjectIds
                    .Select(id => new SignalContributionDto
                    {
                        RootProjectId = id,
                        RootProjectName = labels.Projects.GetValueOrDefault(id) ?? "—"
                    })
                    .ToList()),

        _ => KindParts.None
    };

    private static SignalDto ToDto(PlanSignal s, Labels labels, DateTimeOffset? lastVisit)
    {
        var ack = s.CurrentAcknowledgement;
        var parts = PartsFor(s, labels);

        return new SignalDto
        {
            Id = s.Id,
            Kind = s.Kind,
            Tier = s.Tier,
            Shape = s.Shape,
            SubjectId = s.SubjectId,
            Zone = s.Zone,
            DeadlineAt = s.DeadlineAt,
            Magnitude = s.Magnitude,
            HardCommitted = s.HardCommitted,
            FirstDetectedAt = s.FirstDetectedAt,
            LastObservedAt = s.LastObservedAt,
            IsUnseen = lastVisit is null || s.FirstDetectedAt > lastVisit,
            IsAcknowledged = s.IsAcknowledged,
            AcknowledgedBy = ack?.By,
            AcknowledgedAt = ack?.At,
            AcknowledgedReason = ack?.Reason,
            // Alphabetical and without any figure: informative, and structurally
            // incapable of reading as a ranking of people.
            MemberNames = s.MemberSubjectIds
                .Select(id => labels.Resources.GetValueOrDefault(id))
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.CurrentCulture)
                .ToList(),

            RoleName = parts.RoleName,
            ResourceName = parts.ResourceName,
            RootProjectName = parts.RootProjectName,
            OwnerName = parts.OwnerName,
            Link = parts.Link,
            Contributions = parts.Contributions
        };
    }

    private SignalChangeDto ToChangeDto(PlanSignal s, Labels labels)
    {
        var projected = ToDto(s, labels, lastVisit: null);
        return new SignalChangeDto
        {
            SignalId = s.Id,
            Kind = s.Kind,
            Change = s.LastChange,
            At = s.LastChangedAt ?? s.LastObservedAt,
            RoleName = projected.RoleName,
            ResourceName = projected.ResourceName,
            RootProjectName = projected.RootProjectName,
            Magnitude = s.Magnitude,
            PreviousMagnitude = s.PreviousMagnitude,
            Zone = s.Zone,
            PreviousZone = s.PreviousZone,
            Link = projected.Link
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<ServiceResult<SignalDto>> SingleAsync(PlanSignal signal, CancellationToken ct)
    {
        var labels = await ResolveLabelsAsync([signal], ct);
        return ServiceResult<SignalDto>.Success(ToDto(signal, labels, await LastVisitedAtAsync(ct)));
    }

    private async Task<DateTimeOffset?> LastVisitedAtAsync(CancellationToken ct)
    {
        var sub = currentUser.User.Sub;
        if (string.IsNullOrWhiteSpace(sub)) return null;

        return await db.SignalVisits.AsNoTracking()
            .Where(v => v.UserSub == sub)
            .Select(v => (DateTimeOffset?)v.LastVisitedAt)
            .FirstOrDefaultAsync(ct);
    }

    // The author of an organizational decision, recorded so "chi l'ha accettata"
    // has an answer.
    private string Author()
    {
        var user = currentUser.User;
        return string.IsNullOrWhiteSpace(user.Email) ? user.Sub : user.Email;
    }
}
