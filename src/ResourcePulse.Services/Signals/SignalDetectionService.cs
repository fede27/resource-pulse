using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Demands;
using ResourcePulse.Services.Load;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Signals;

// The detector (ADR-0032 §9). Reads the plan through the SAME batch read models
// the boards use (ADR-0028) and reconciles what it measures against what is
// already on file.
//
// RANGE SCOPING — the one thing to understand before changing anything here.
// Demand/coverage reconciliation is range-scoped by construction (covered hours =
// % × capacity intersected with the range). Reconciling only over the committing
// horizon would therefore report a huge gap on every in-flight project, because
// the coverage already delivered outside the window would not count. So the
// reconciliation window is the UNION of the active roots' planned windows, walked
// in ≤366-day chunks (the read model's cap) and summed: covered hours are
// additive over disjoint ranges, required hours are not summed at all. Admission
// to the queue is a separate question, decided by the DEADLINE (ADR-0033 §8).
public sealed class SignalDetectionService(
    ResourcePulseDbContext db,
    ILoadQueryService loadQuery,
    IAllocationService allocations,
    ICapacityQueryService capacity,
    ITimeFenceConfigurationService fence,
    ILoadBandConfigurationService bands,
    ICommitmentPolicyService commitmentPolicy,
    ISignalPolicyService signalPolicy,
    IRepository<SignalSweepState, Guid> sweepStates,
    TimeProvider clock) : ISignalDetectionService
{
    // The read models refuse anything wider; the union of project windows is
    // walked in chunks of this size.
    private const int ChunkDays = 366;

    // Safety bound on the reconciliation walk. A portfolio spanning more than
    // this is pathological; the walk keeps the MOST RECENT chunks, since a gap on
    // work that finished years ago is not triage.
    private const int MaxChunks = 8;

    public async Task<ServiceResult<SignalSweepResult>> SweepAsync(DateOnly today, CancellationToken ct = default)
    {
        var context = await BuildContextAsync(today, ct);
        var detected = await DetectAsync(context, ct);

        // Admission (ADR-0033 §8): the deadline must fall inside the committing
        // horizon — or there must be no deadline at all, in which case the signal
        // enters on tier alone.
        var admitted = detected
            .Where(d => SignalZones.IsInCommittingHorizon(d.Observation.Zone))
            .ToList();

        var result = await ReconcileAsync(admitted, context, ct);
        return ServiceResult<SignalSweepResult>.Success(result);
    }

    private async Task<DetectionContext> BuildContextAsync(DateOnly today, CancellationToken ct)
    {
        var fenceConfig = await fence.GetConfigurationAsync(ct);
        var bandConfig = await bands.GetConfigurationAsync(ct);
        var commitment = await commitmentPolicy.GetConfigurationAsync(ct);
        var policy = await signalPolicy.GetConfigurationAsync(ct);

        return new DetectionContext(today, fenceConfig.ComputeBoundaries(today), bandConfig, commitment, policy);
    }

    // Everything the plan currently says, before anything is compared with what is
    // already on file. Shared by the sweep and the resolve-only hook precisely so
    // there is ONE answer to "what exists right now".
    private async Task<List<DetectedSignal>> DetectAsync(DetectionContext context, CancellationToken ct)
    {
        var detected = new List<DetectedSignal>();
        detected.AddRange(await DetectGapsAsync(context, ct));
        detected.AddRange(await DetectTentativeInFrozenAsync(context, ct));
        detected.AddRange(await DetectOvercommitAndSlackAsync(context, ct));
        detected.AddRange(await DetectHygieneAsync(context, ct));
        return detected;
    }

    public async Task<ServiceResult<int>> ResolveStaleAsync(
        SignalTouch touch, DateOnly today, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(touch);
        if (touch.IsEmpty) return ServiceResult<int>.Success(0);

        var candidates = await db.PlanSignals
            .Where(s => s.Detection == SignalDetection.Live && s.SubjectId != null)
            .ToListAsync(ct);

        candidates = candidates.Where(s => Touches(s, touch)).ToList();
        if (candidates.Count == 0) return ServiceResult<int>.Success(0);

        // FULL detection, PARTIAL application. Deliberately not a second, narrower
        // "does this condition still hold?" implementation per kind: a parallel
        // predicate is exactly the sort of duplicate that drifts from the detector
        // and starts resolving things that are still true. One code path decides
        // what exists; this method only chooses which of its conclusions to apply.
        var detected = await DetectAsync(await BuildContextAsync(today, ct), ct);
        var detectedKeys = detected
            .Where(d => SignalZones.IsInCommittingHorizon(d.Observation.Zone))
            .Select(d => (d.Kind, d.SubjectId))
            .ToHashSet();

        var now = clock.GetUtcNow();
        var resolved = 0;
        foreach (var signal in candidates.Where(s => !detectedKeys.Contains((s.Kind, s.SubjectId))))
        {
            signal.Resolve(now);
            resolved++;
        }

        if (resolved > 0) await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(resolved);
    }

    private static bool Touches(PlanSignal signal, SignalTouch touch) => signal.Kind switch
    {
        SignalKind.Gap => touch.DemandIds.Contains(signal.SubjectId!.Value),
        SignalKind.TentativeInFrozen => touch.AllocationIds.Contains(signal.SubjectId!.Value),
        SignalKind.Overcommit => touch.ResourceIds.Contains(signal.SubjectId!.Value),
        // Aggregates have no subject and are never resolved by this path: their
        // membership is a portfolio-wide fact, not something one gesture settles.
        _ => false
    };

    // ── Reconciliation ───────────────────────────────────────────────────────

    private async Task<SignalSweepResult> ReconcileAsync(
        IReadOnlyList<DetectedSignal> detected, DetectionContext ctx, CancellationToken ct)
    {
        // Through the clock, not DateTimeOffset.UtcNow: the retention purge is a
        // comparison against wall time, and a pass whose timestamps cannot be
        // controlled cannot be tested for it.
        var now = clock.GetUtcNow();

        var live = await db.PlanSignals
            .Where(s => s.Detection == SignalDetection.Live)
            .ToListAsync(ct);

        var liveByKey = live.ToDictionary(s => (s.Kind, s.SubjectId));
        var seen = new HashSet<(SignalKind, Guid?)>();
        int created = 0, worsened = 0;

        foreach (var d in detected)
        {
            var key = (d.Kind, d.SubjectId);
            seen.Add(key);

            if (liveByKey.TryGetValue(key, out var existing))
            {
                if (existing.Observe(d.Observation, now) != SignalChange.None) worsened++;
            }
            else
            {
                db.PlanSignals.Add(PlanSignal.Detect(d.Kind, d.SubjectId, d.Observation, now));
                created++;
            }
        }

        // Anything live that the pass did not observe is gone. Resolving is the
        // detector's job alone — the dashboard's inline confirm mutates the plan
        // and lets this line notice (ADR-0032 §3).
        var resolved = 0;
        foreach (var signal in live.Where(s => !seen.Contains((s.Kind, s.SubjectId))))
        {
            signal.Resolve(now);
            resolved++;
        }

        // Resolved rows ARE the change feed's history, so this is depth, not
        // housekeeping.
        var cutoff = now.AddDays(-ctx.Policy.ResolvedRetentionDays);
        var expired = await db.PlanSignals
            .Where(s => s.Detection == SignalDetection.Resolved && s.ResolvedAt != null && s.ResolvedAt < cutoff)
            .ToListAsync(ct);
        db.PlanSignals.RemoveRange(expired);

        var liveCount = live.Count - resolved + created;

        // Seeded through the shared helper, and therefore in its OWN save: two
        // processes racing on a per-tenant singleton must not take the whole
        // reconciliation down with them (see SingletonSeed).
        var state = await SingletonSeed.GetOrSeedAsync(
            db, sweepStates,
            token => db.SignalSweepStates.FirstOrDefaultAsync(token),
            SignalSweepState.CreateUnswept,
            ct);

        state.RecordSweep(now, liveCount);

        await db.SaveChangesAsync(ct);

        return new SignalSweepResult
        {
            Today = ctx.Today,
            HorizonEnd = ctx.Boundaries.SlushyUntil,
            Detected = detected.Count,
            Created = created,
            Resolved = resolved,
            Worsened = worsened,
            Purged = expired.Count,
            LiveCount = liveCount
        };
    }

    // ── Gap (breach, per demand) ─────────────────────────────────────────────

    private async Task<IReadOnlyList<DetectedSignal>> DetectGapsAsync(DetectionContext ctx, CancellationToken ct)
    {
        var roots = await ActiveRootsAsync(ct);
        if (roots.Count == 0) return [];

        // Covered hours summed across the chunks; required hours taken once.
        var covered = new Dictionary<Guid, TimeSpan>();
        var byDemand = new Dictionary<Guid, DemandCoverageDto>();

        foreach (var (from, to) in ReconciliationChunks(roots, ctx.Today))
        {
            var slice = await loadQuery.GetDemandCoverageInRangeAsync(from, to, ct);
            if (!slice.IsSuccess || slice.Value is null) continue;

            foreach (var row in slice.Value)
            {
                byDemand[row.DemandId] = row;
                covered[row.DemandId] = covered.GetValueOrDefault(row.DemandId) + row.CoveredHours;
            }
        }

        if (byDemand.Count == 0) return [];

        var deadlines = await DemandDeadlinesAsync(byDemand.Keys, ctx, ct);
        var signals = new List<DetectedSignal>();

        foreach (var (demandId, row) in byDemand)
        {
            // Best-effort demands have NO target and therefore no gap (revision
            // §7). They are not a gap of zero and must never be triaged as one.
            if (row.RequiredHours is not TimeSpan required) continue;

            var gap = required - covered.GetValueOrDefault(demandId);
            if (gap <= TimeSpan.Zero) continue;

            if (!deadlines.TryGetValue(demandId, out var info)) continue;

            signals.Add(new DetectedSignal(
                SignalKind.Gap,
                demandId,
                SignalObservation.For(
                    info.Deadline, ctx.Today, ctx.Boundaries,
                    magnitude: (decimal)gap.TotalHours,
                    hardCommitted: info.HardCommitted,
                    touchedRootProjectIds: [row.RootProjectId])));
        }

        return signals;
    }

    // ── Tentative crossing the frozen fence (breach, per coverage block) ─────

    private async Task<IReadOnlyList<DetectedSignal>> DetectTentativeInFrozenAsync(
        DetectionContext ctx, CancellationToken ct)
    {
        // "Crossed the fence" means the window OVERLAPS [today, frozenUntil], not
        // merely that it starts before the boundary.
        var inFrozen = await allocations.GetInRangeAsync(ctx.Today, ctx.Boundaries.FrozenUntil, ct);
        if (!inFrozen.IsSuccess || inFrozen.Value is null) return [];

        var tentative = inFrozen.Value.Where(a => a.Status == AllocationStatus.Tentative).ToList();
        if (tentative.Count == 0) return [];

        // Hours are the reconciliation truth (ADR-0026), so the magnitude is
        // resolved hours rather than a rate: one batch capacity read buys that.
        var resourceIds = tentative.Select(a => a.ResourceId).Distinct().ToList();
        var capacities = await capacity.GetSegmentsForResourcesAsync(
            resourceIds, ctx.Today, ctx.Boundaries.FrozenUntil, ct);

        var hardCommitted = await HardCommittedRootsAsync(ctx, ct);

        return tentative.Select(a =>
        {
            var hours = ResolvedHoursInFrozen(a, capacities, ctx);
            // Its identity IS being in the frozen zone, so the deadline is the
            // moment it becomes untouchable: now, or when it starts if later.
            var deadline = a.PeriodStart < ctx.Today ? ctx.Today : a.PeriodStart;

            return new DetectedSignal(
                SignalKind.TentativeInFrozen,
                a.Id,
                SignalObservation.For(
                    deadline, ctx.Today, ctx.Boundaries,
                    magnitude: hours,
                    hardCommitted: hardCommitted.Contains(a.RootProjectId),
                    touchedRootProjectIds: [a.RootProjectId]));
        }).ToList();
    }

    private static decimal ResolvedHoursInFrozen(
        AllocationReadDto a,
        ServiceResult<IReadOnlyList<ResourceCapacityDto>> capacities,
        DetectionContext ctx)
    {
        if (!capacities.IsSuccess || capacities.Value is null) return 0m;

        var segments = capacities.Value.FirstOrDefault(c => c.ResourceId == a.ResourceId)?.Segments;
        if (segments is null) return 0m;

        var from = a.PeriodStart < ctx.Today ? ctx.Today : a.PeriodStart;
        var to = a.PeriodEnd > ctx.Boundaries.FrozenUntil ? ctx.Boundaries.FrozenUntil : a.PeriodEnd;

        var hours = 0m;
        foreach (var s in segments)
        {
            var overlapFrom = s.From > from ? s.From : from;
            var overlapTo = s.To < to ? s.To : to;
            if (overlapFrom > overlapTo) continue;

            var days = overlapTo.DayNumber - overlapFrom.DayNumber + 1;
            hours += (decimal)s.HoursPerDay.TotalHours * days;
        }

        return Math.Round(hours * a.AllocationPercent / 100m, 2);
    }

    // ── Overcommit (breach, per resource) + UnderBand (slack, aggregated) ────

    private async Task<IReadOnlyList<DetectedSignal>> DetectOvercommitAndSlackAsync(
        DetectionContext ctx, CancellationToken ct)
    {
        // Hard-only, like the sustainability verdict: a tentative block is not a
        // commitment and must not be counted as one.
        var profiles = await loadQuery.GetCommitmentProfilesForResourcesAsync(
            null, ctx.Today, ctx.Boundaries.SlushyUntil, AllocationStatus.Hard, ct);

        if (!profiles.IsSuccess || profiles.Value is null) return [];

        var signals = new List<DetectedSignal>();
        var underBand = new List<Guid>();

        foreach (var profile in profiles.Value)
        {
            var breaching = profile.Segments
                .Where(s => s.Percent >= ctx.Bands.OverloadFloor)
                .OrderBy(s => s.From)
                .ToList();

            if (breaching.Count > 0)
            {
                var first = breaching[0];
                var peak = breaching.Max(s => s.Percent);

                signals.Add(new DetectedSignal(
                    SignalKind.Overcommit,
                    profile.ResourceId,
                    SignalObservation.For(
                        // The deadline is when the overload STARTS: after that it
                        // has happened. Not a decision date — hence DeadlineAt.
                        first.From < ctx.Today ? ctx.Today : first.From,
                        ctx.Today, ctx.Boundaries,
                        magnitude: peak - ctx.Bands.OverloadFloor,
                        hardCommitted: true,
                        touchedRootProjectIds: breaching
                            .SelectMany(s => s.ByProject.Select(p => p.ProjectNodeId))
                            .Distinct().ToList())));
            }

            // A 0% segment is ABSENCE OF DATA, not under-load — the person simply
            // has no allocations in the window. Counting it would turn "we have
            // not planned this yet" into "this person is idle".
            var slack = profile.Segments.Any(s => s.Percent > 0 && s.Percent < ctx.Bands.HealthyFloor);
            if (slack) underBand.Add(profile.ResourceId);
        }

        if (underBand.Count > 0)
        {
            signals.Add(new DetectedSignal(
                SignalKind.UnderBand,
                null,
                SignalObservation.For(
                    // No deadline: unused capacity is not a thing that falls due.
                    deadline: null, ctx.Today, ctx.Boundaries,
                    // A CARDINALITY, never a utilization rate — which is what
                    // structurally prevents this from becoming a ranking of people.
                    magnitude: underBand.Count,
                    memberSubjectIds: underBand)));
        }

        return signals;
    }

    // ── Hygiene (aggregated) ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<DetectedSignal>> DetectHygieneAsync(DetectionContext ctx, CancellationToken ct)
    {
        var signals = new List<DetectedSignal>();

        // Demand still open on a Closed/Cancelled root. I4 forbids covering it,
        // and demands/coverage excludes those roots — so without this the data
        // exists and nothing anywhere can see it.
        var closedRootIds = await db.ProjectNodes
            .Where(n => n.ParentId == null &&
                        (n.Status == ProjectStatus.Closed || n.Status == ProjectStatus.Cancelled))
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (closedRootIds.Count > 0)
        {
            // Matched on the root id parsed from the materialized Path rather than
            // a StartsWith over a closure list: one prefix predicate per root does
            // not translate, and the root set is small enough that projecting
            // (demand, path) and grouping in memory is both simpler and cheaper.
            var closed = closedRootIds.ToHashSet();
            var demandPaths = await db.Demands
                .Join(db.ProjectNodes, d => d.ProjectNodeId, n => n.Id, (d, n) => new { d.Id, n.Path })
                .ToListAsync(ct);

            var stranded = demandPaths
                .Where(x => closed.Contains(RootIdFromPath(x.Path)))
                .Select(x => x.Id)
                .ToList();

            AddAggregate(signals, ctx, SignalKind.DemandOnClosedRoot, stranded);
        }

        // Coverage whose window falls outside its node's planned window. Nothing
        // constrains the two, so they drift.
        var outOfWindow = await db.Allocations
            .Where(a => db.ProjectNodes.Any(n =>
                n.Id == a.ProjectNodeId &&
                ((n.PlannedStart != null && a.PeriodStart < n.PlannedStart) ||
                 (n.PlannedEnd != null && a.PeriodEnd > n.PlannedEnd))))
            .Select(a => a.Id)
            .ToListAsync(ct);

        AddAggregate(signals, ctx, SignalKind.CoverageOutOfWindow, outOfWindow);

        // Demand on a node with no planned start ANYWHERE up to the root: no
        // deadline is derivable, so it cannot be placed in a zone at all
        // (ADR-0033 §9).
        var undated = await UndatedDemandIdsAsync(ct);
        AddAggregate(signals, ctx, SignalKind.DemandOnUndatedNode, undated);

        // A deactivated resource still carrying future coverage. I3 is evaluated
        // at creation only, so deactivating someone afterwards leaves coverage
        // behind that keeps counting hours nobody will work.
        var inactive = await db.Allocations
            .Where(a => a.PeriodEnd >= ctx.Today &&
                        db.Resources.Any(r => r.Id == a.ResourceId && !r.IsActive))
            .Select(a => a.Id)
            .ToListAsync(ct);

        AddAggregate(signals, ctx, SignalKind.InactiveWithCoverage, inactive);

        // No default calendar: new resources have no starting pattern. A
        // configuration-completeness fact with no subjects to enumerate.
        var hasCalendars = await db.BusinessCalendars.AnyAsync(ct);
        var hasDefault = await db.BusinessCalendars.AnyAsync(c => c.IsDefault, ct);
        if (hasCalendars && !hasDefault)
            AddAggregate(signals, ctx, SignalKind.NoDefaultCalendar, [Guid.Empty], forceCount: 1);

        return signals;
    }

    private static void AddAggregate(
        List<DetectedSignal> signals,
        DetectionContext ctx,
        SignalKind kind,
        IReadOnlyList<Guid> members,
        int? forceCount = null)
    {
        var count = forceCount ?? members.Count;
        if (count == 0) return;

        signals.Add(new DetectedSignal(
            kind,
            null,
            SignalObservation.For(
                // Hygiene has no deadline. The prototype hardcodes 'frozen' on
                // these rows, inventing urgency; saying "none" is more honest and
                // the tier already keeps them below the breaches.
                deadline: null, ctx.Today, ctx.Boundaries,
                magnitude: count,
                memberSubjectIds: forceCount is null ? members : [])));
    }

    // ── Deadlines ────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, DemandDeadline>> DemandDeadlinesAsync(
        IEnumerable<Guid> demandIds, DetectionContext ctx, CancellationToken ct)
    {
        var ids = demandIds.ToList();

        var rows = await db.Demands
            .Where(d => ids.Contains(d.Id))
            .Join(db.ProjectNodes, d => d.ProjectNodeId, n => n.Id,
                (d, n) => new { d.Id, d.DecideBy, n.Path, NodeStart = n.PlannedStart })
            .ToListAsync(ct);

        var rootIds = rows.Select(r => RootIdFromPath(r.Path)).Distinct().ToList();
        var roots = await db.ProjectNodes
            .Where(n => rootIds.Contains(n.Id))
            .Select(n => new { n.Id, n.PlannedStart, n.CommitmentLevel })
            .ToListAsync(ct);

        var rootById = roots.ToDictionary(r => r.Id);
        var result = new Dictionary<Guid, DemandDeadline>();

        foreach (var row in rows)
        {
            var rootId = RootIdFromPath(row.Path);
            rootById.TryGetValue(rootId, out var root);

            // The explicit override wins; otherwise derive from the node's planned
            // start, falling back to the ROOT's (ADR-0033 §3). The fallback is not
            // a parent-child date invariant — it is the resolution of a single
            // read, and without it an undated phase on a perfectly dated project
            // would surface as model hygiene, which is noise rather than
            // information.
            var deadline = row.DecideBy
                ?? ctx.Policy.DeriveDeadline(row.NodeStart ?? root?.PlannedStart);

            var hardCommitted = root?.CommitmentLevel is CommitmentLevel level &&
                                ctx.Commitment.IsHardCommitted(level);

            result[row.Id] = new DemandDeadline(deadline, hardCommitted);
        }

        return result;
    }

    private async Task<IReadOnlyList<Guid>> UndatedDemandIdsAsync(CancellationToken ct)
    {
        var rows = await db.Demands
            .Join(db.ProjectNodes, d => d.ProjectNodeId, n => n.Id,
                (d, n) => new { d.Id, n.Path, NodeStart = n.PlannedStart })
            .Where(x => x.NodeStart == null)
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var rootIds = rows.Select(r => RootIdFromPath(r.Path)).Distinct().ToList();
        var datedRoots = await db.ProjectNodes
            .Where(n => rootIds.Contains(n.Id) && n.PlannedStart != null)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var dated = datedRoots.ToHashSet();
        return rows.Where(r => !dated.Contains(RootIdFromPath(r.Path))).Select(r => r.Id).ToList();
    }

    // ── Shared reads ─────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ActiveRoot>> ActiveRootsAsync(CancellationToken ct) =>
        await db.ProjectNodes
            .Where(n => n.ParentId == null &&
                        n.Status != ProjectStatus.Closed &&
                        n.Status != ProjectStatus.Cancelled)
            .Select(n => new ActiveRoot(n.Id, n.PlannedStart, n.PlannedEnd, n.CommitmentLevel))
            .ToListAsync(ct);

    private async Task<HashSet<Guid>> HardCommittedRootsAsync(DetectionContext ctx, CancellationToken ct)
    {
        var roots = await ActiveRootsAsync(ct);
        return roots
            .Where(r => r.CommitmentLevel is CommitmentLevel level && ctx.Commitment.IsHardCommitted(level))
            .Select(r => r.Id)
            .ToHashSet();
    }

    // The reconciliation window: the union of the active roots' planned windows,
    // always including the committing horizon, walked in ≤366-day chunks because
    // the read model refuses anything wider. Covered hours are additive across
    // disjoint chunks; required hours are read once per demand.
    private static IEnumerable<(DateOnly From, DateOnly To)> ReconciliationChunks(
        IReadOnlyList<ActiveRoot> roots, DateOnly today)
    {
        var starts = roots.Where(r => r.PlannedStart is not null).Select(r => r.PlannedStart!.Value).ToList();
        var ends = roots.Where(r => r.PlannedEnd is not null).Select(r => r.PlannedEnd!.Value).ToList();

        var from = starts.Count > 0 ? starts.Min() : today;
        var to = ends.Count > 0 ? ends.Max() : today.AddDays(ChunkDays - 1);

        if (from > today) from = today;
        if (to < today) to = today;

        var chunks = new List<(DateOnly, DateOnly)>();
        var cursor = from;
        while (cursor <= to)
        {
            var end = cursor.AddDays(ChunkDays - 1);
            if (end > to) end = to;
            chunks.Add((cursor, end));
            cursor = end.AddDays(1);
        }

        // Keep the most recent chunks: a gap on work that finished years ago is
        // not triage, and the walk must stay bounded.
        return chunks.Count <= MaxChunks ? chunks : chunks.TakeLast(MaxChunks);
    }

    // Root project node id = first segment of the materialized Path "/{rootId}/...".
    private static Guid RootIdFromPath(string path)
    {
        var first = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
        return Guid.Parse(first);
    }

    private sealed record ActiveRoot(Guid Id, DateOnly? PlannedStart, DateOnly? PlannedEnd, CommitmentLevel? CommitmentLevel);

    private sealed record DemandDeadline(DateOnly? Deadline, bool HardCommitted);

    private sealed record DetectionContext(
        DateOnly Today,
        FenceBoundaries Boundaries,
        LoadBandConfiguration Bands,
        CommitmentPolicyConfiguration Commitment,
        SignalPolicy Policy);
}
