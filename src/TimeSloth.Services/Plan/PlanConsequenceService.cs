using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Results;
using TimeSloth.Domain.Allocations;
using TimeSloth.Domain.Capacity;
using TimeSloth.Domain.Configuration;
using TimeSloth.Domain.Projects;
using TimeSloth.Persistence;
using TimeSloth.Services.Capacity;
using TimeSloth.Services.Configuration;
using TimeSloth.Services.Shared;

namespace TimeSloth.Services.Plan;

// What pricing needs that is not the plan: the bands, the fence, today.
public sealed record PlanPricing(LoadBandConfiguration Bands, FenceBoundaries Fence, DateOnly Today);

public interface IPlanConsequenceService
{
    // Must run BEFORE the command mutates anything. The configuration reads are
    // get-or-seed, and a seed is a SaveChanges: issued after the kernel has
    // touched a tracked aggregate, it would commit a dryRun.
    Task<PlanPricing> BeginAsync(CancellationToken ct = default);

    // `moved` are the tracked blocks AFTER the command re-snapped them, and it
    // must be called before the command saves or clears the tracker: the
    // "before" is whatever the database still holds.
    Task<ServiceResult<PlanConsequence>> PriceAsync(
        PlanPricing pricing,
        IReadOnlyList<Allocation> moved,
        IReadOnlyList<PlanReferentChange> referents,
        CancellationToken ct = default);
}

// Prices a node move (ADR-0036). Composition only: the arithmetic is the pure
// PlanConsequenceCalculator and the reconciliation is LoadCalculator's, the same
// one the boards and the detector read — so a demand this calls "covered" is one
// the sweep will not flag.
public sealed class PlanConsequenceService(
    TimeSlothDbContext db,
    ICapacityQueryService capacity,
    ILoadBandConfigurationService bands,
    ITimeFenceConfigurationService fence,
    TimeProvider clock) : IPlanConsequenceService
{
    // Which blocks count toward a person's band. Hard only, as on the Projects
    // board and in the Overcommit detector: a proposal must not flip someone
    // into overload. One place, so the three cannot drift apart.
    private const AllocationStatus BandBasis = AllocationStatus.Hard;

    public async Task<PlanPricing> BeginAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var bandConfig = await bands.GetConfigurationAsync(ct);
        var fenceConfig = await fence.GetConfigurationAsync(ct);
        return new PlanPricing(bandConfig, fenceConfig.ComputeBoundaries(today), today);
    }

    public async Task<ServiceResult<PlanConsequence>> PriceAsync(
        PlanPricing pricing,
        IReadOnlyList<Allocation> moved,
        IReadOnlyList<PlanReferentChange> referents,
        CancellationToken ct = default)
    {
        var movedIds = moved.Select(a => a.Id).ToList();
        var storedById = await db.Allocations.AsNoTracking()
            .Where(a => movedIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var shifts = moved
            .Where(a => storedById.ContainsKey(a.Id))
            .Select(a => new BlockShift(storedById[a.Id], a))
            .ToList();

        var changedDays = shifts
            .SelectMany(PlanConsequenceCalculator.ChangedDays)
            .Select(x => x.Date)
            .ToList();

        if (changedDays.Count == 0)
            return ServiceResult<PlanConsequence>.Success(new PlanConsequence
            {
                Today = pricing.Today,
                FrozenUntil = pricing.Fence.FrozenUntil,
                TouchesFrozen = referents.Any(r => MovesThroughFrozen(r, pricing))
            });

        var changedFrom = changedDays.Min();
        var changedTo = changedDays.Max();
        var resourceIds = shifts.Select(s => s.After.ResourceId).Distinct().ToList();
        var demandIds = shifts.Select(s => s.After.DemandId).Distinct().ToList();

        // Every block of the touched people over the changed span — ALL their
        // projects, which is the whole point: the cost lands where the planner
        // is not looking.
        var personBlocksBefore = await db.Allocations.AsNoTracking()
            .Where(a => resourceIds.Contains(a.ResourceId)
                     && a.PeriodStart <= changedTo && a.PeriodEnd >= changedFrom)
            .ToListAsync(ct);
        var personBlocksAfter = After(personBlocksBefore, movedIds, moved);

        var demandBlocksBefore = await db.Allocations.AsNoTracking()
            .Where(a => demandIds.Contains(a.DemandId))
            .ToListAsync(ct);
        var demandBlocksAfter = After(demandBlocksBefore, movedIds, moved);

        // The demands are reconciled over the whole extent of their coverage,
        // not over a board range: "covered" here must mean what it means to the
        // sweep, or the confirmation would promise something triage then denies.
        var extent = demandBlocksBefore.Concat(demandBlocksAfter).ToList();
        var reconcileFrom = extent.Min(a => a.PeriodStart);
        var reconcileTo = extent.Max(a => a.PeriodEnd);

        var capacityIds = resourceIds.Concat(extent.Select(a => a.ResourceId)).Distinct().ToList();
        var capacityResult = await LoadCapacityAsync(
            capacityIds,
            reconcileFrom < changedFrom ? reconcileFrom : changedFrom,
            reconcileTo > changedTo ? reconcileTo : changedTo,
            ct);
        if (capacityResult.IsFailure)
            return ServiceResult<PlanConsequence>.Failure(capacityResult.Error!);
        var capacityByDay = capacityResult.Value;

        var demands = await db.Demands.AsNoTracking().Where(d => demandIds.Contains(d.Id)).ToListAsync(ct);
        var coverageBefore = LoadCalculator
            .CoverageForDemands(demands, demandBlocksBefore, capacityByDay, reconcileFrom, reconcileTo)
            .ToDictionary(c => c.DemandId);
        var coverageAfter = LoadCalculator
            .CoverageForDemands(demands, demandBlocksAfter, capacityByDay, reconcileFrom, reconcileTo)
            .ToDictionary(c => c.DemandId);

        var people = await db.Resources.AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name, r.AvailableFrom, r.AvailableUntil })
            .ToDictionaryAsync(r => r.Id, ct);

        var roleIds = demands.Select(d => d.RoleId).Distinct().ToList();
        var roleNames = await db.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var nodeIds = personBlocksBefore.Select(a => a.ProjectNodeId)
            .Concat(moved.Select(a => a.ProjectNodeId))
            .Concat(referents.Select(r => r.Id))
            .Distinct().ToList();
        var nodes = await db.ProjectNodes.AsNoTracking()
            .Where(p => nodeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Path })
            .ToListAsync(ct);
        var nodeNames = nodes.ToDictionary(n => n.Id, n => n.Name);
        var rootByNode = nodes.ToDictionary(n => n.Id, n => ProjectNodePath.RootId(n.Path));
        var rootIds = rootByNode.Values.Distinct().ToList();
        var rootNames = await db.ProjectNodes.AsNoTracking()
            .Where(p => rootIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var gestureRoots = referents
            .Where(r => rootByNode.ContainsKey(r.Id))
            .Select(r => rootByNode[r.Id])
            .ToHashSet();

        var closures = (await db.CompanyClosures.AsNoTracking()
                .Where(c => c.DateFrom <= changedTo && c.DateTo >= changedFrom)
                .Select(c => new { c.DateFrom, c.DateTo, c.Reason })
                .ToListAsync(ct))
            .Select(c => (c.DateFrom, c.DateTo, c.Reason))
            .ToList();

        var personRows = new List<PlanPersonConsequence>();
        foreach (var resourceId in resourceIds)
        {
            Func<DateOnly, TimeSpan> capacityOn = d =>
                capacityByDay.TryGetValue((resourceId, d), out var c) ? c : TimeSpan.Zero;
            people.TryGetValue(resourceId, out var person);

            var priced = PlanConsequenceCalculator.ForPerson(
                shifts.Where(s => s.After.ResourceId == resourceId).ToList(),
                personBlocksBefore.Where(a => a.ResourceId == resourceId && a.Status == BandBasis).ToList(),
                personBlocksAfter.Where(a => a.ResourceId == resourceId && a.Status == BandBasis).ToList(),
                capacityOn,
                rootByNode,
                gestureRoots,
                closures,
                person?.AvailableFrom,
                person?.AvailableUntil);

            personRows.Add(new PlanPersonConsequence
            {
                ResourceId = resourceId,
                ResourceName = person?.Name ?? string.Empty,
                HoursDelta = priced.HoursDelta,
                AvailableFrom = person?.AvailableFrom,
                AvailableUntil = person?.AvailableUntil,
                Intervals = priced.Intervals.Select(i => new PlanLoadInterval
                {
                    From = i.From,
                    To = i.To,
                    PercentBefore = i.PercentBefore,
                    PercentAfter = i.PercentAfter,
                    BandChanged = pricing.Bands.Resolve(i.PercentBefore).LowerBound
                               != pricing.Bands.Resolve(i.PercentAfter).LowerBound,
                    OverloadedAfter = i.PercentAfter >= pricing.Bands.OverloadFloor,
                    OtherProjects = i.OtherRootIds
                        .Select(id => new PlanProjectRef { Id = id, Name = rootNames.GetValueOrDefault(id, string.Empty) })
                        .OrderBy(p => p.Name)
                        .ToList()
                }).ToList(),
                ZeroCapacity = priced.ZeroCapacity
                    .Select(z => new PlanZeroCapacityRun { From = z.From, To = z.To, Reason = z.Reason })
                    .ToList(),
                BeyondAvailability = priced.Beyond is { } b
                    ? new PlanBeyondAvailability { From = b.From, To = b.To, Days = b.Days, Hours = b.Hours }
                    : null
            });
        }

        var demandRows = demands.Select(d =>
        {
            var before = coverageBefore[d.Id];
            var after = coverageAfter[d.Id];
            return new PlanDemandConsequence
            {
                DemandId = d.Id,
                ProjectNodeId = d.ProjectNodeId,
                NodeName = nodeNames.GetValueOrDefault(d.ProjectNodeId, string.Empty),
                RoleId = d.RoleId,
                RoleName = roleNames.GetValueOrDefault(d.RoleId, string.Empty),
                RequiredHours = d.RequiredHours,
                CoveredBefore = before.CoveredHours,
                CoveredAfter = after.CoveredHours,
                GapBefore = before.GapHours,
                GapAfter = after.GapHours,
                // Best-effort has no gap, so it has no state to change.
                StateChanged = before.GapHours is { } gb && after.GapHours is { } ga
                            && (gb > TimeSpan.Zero) != (ga > TimeSpan.Zero)
            };
        });

        return ServiceResult<PlanConsequence>.Success(new PlanConsequence
        {
            Today = pricing.Today,
            FrozenUntil = pricing.Fence.FrozenUntil,
            TouchesFrozen = changedDays.Any(d => InFrozen(d, pricing))
                         || referents.Any(r => MovesThroughFrozen(r, pricing)),
            Blocks = shifts
                .Select(s => new PlanBlockShift
                {
                    Id = s.After.Id,
                    ResourceId = s.After.ResourceId,
                    DemandId = s.After.DemandId,
                    AllocationPercent = s.After.AllocationPercent,
                    Status = s.After.Status,
                    OldStart = s.Before.PeriodStart,
                    OldEnd = s.Before.PeriodEnd,
                    NewStart = s.After.PeriodStart,
                    NewEnd = s.After.PeriodEnd,
                    HoursDelta = PlanConsequenceCalculator.HoursDelta(s, d =>
                        capacityByDay.TryGetValue((s.After.ResourceId, d), out var c) ? c : TimeSpan.Zero)
                })
                .OrderBy(b => people.TryGetValue(b.ResourceId, out var p) ? p.Name : string.Empty)
                .ThenBy(b => b.NewStart)
                .ToList(),
            People = personRows.OrderBy(p => p.ResourceName).ToList(),
            Demands = demandRows.OrderBy(d => d.NodeName).ThenBy(d => d.RoleName).ToList()
        });
    }

    // The stored blocks with the moved ones replaced by their in-memory state.
    private static List<Allocation> After(
        IReadOnlyList<Allocation> stored, IReadOnlyCollection<Guid> movedIds, IReadOnlyList<Allocation> moved) =>
        stored.Where(a => !movedIds.Contains(a.Id)).Concat(moved).ToList();

    private static bool InFrozen(DateOnly date, PlanPricing pricing) =>
        date >= pricing.Today && date <= pricing.Fence.FrozenUntil;

    // A node edge moved across, into or out of the frozen zone — even when no
    // block follows it.
    private static bool MovesThroughFrozen(PlanReferentChange r, PlanPricing pricing) =>
        EdgeMovesThroughFrozen(r.OldStart, r.NewStart, pricing) || EdgeMovesThroughFrozen(r.OldEnd, r.NewEnd, pricing);

    private static bool EdgeMovesThroughFrozen(DateOnly? before, DateOnly? after, PlanPricing pricing)
    {
        if (before is not { } b || after is not { } a || b == a) return false;
        var (lo, hi) = b < a ? (b, a) : (a, b);
        return lo <= pricing.Fence.FrozenUntil && hi >= pricing.Today;
    }

    // The capacity read refuses a span wider than DateRangeGuard.MaxDays, so a
    // long demand is read in chunks — the same walk the detector does.
    private async Task<ServiceResult<Dictionary<(Guid ResourceId, DateOnly Date), TimeSpan>>> LoadCapacityAsync(
        IReadOnlyCollection<Guid> resourceIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var byDay = new Dictionary<(Guid ResourceId, DateOnly Date), TimeSpan>();
        for (var chunkFrom = from; chunkFrom <= to; chunkFrom = chunkFrom.AddDays(DateRangeGuard.MaxDays))
        {
            var chunkTo = chunkFrom.AddDays(DateRangeGuard.MaxDays - 1);
            if (chunkTo > to) chunkTo = to;

            var read = await capacity.GetForResourcesAsync(resourceIds, chunkFrom, chunkTo, ct);
            if (read.IsFailure)
                return ServiceResult<Dictionary<(Guid ResourceId, DateOnly Date), TimeSpan>>.Failure(read.Error!);

            foreach (var (resourceId, days) in read.Value)
                foreach (var day in days)
                    byDay[(resourceId, day.Date)] = day.Hours;
        }
        return ServiceResult<Dictionary<(Guid ResourceId, DateOnly Date), TimeSpan>>.Success(byDay);
    }
}
