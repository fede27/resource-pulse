using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;

namespace ResourcePulse.Services.Load;

// Loads allocations for a resource or project node, asks ICapacityQueryService for
// the corresponding capacity, then runs the pure LoadCalculator. The capacity
// service is injected via its interface so a future SnapshotCapacityQueryService
// can be swapped in without touching this code. Project-node load issues capacity
// queries sequentially (ResourcePulseDbContext is pooled — not safe for parallel
// queries on the same instance). See ADR-0010.
public sealed class LiveLoadQueryService(
    ResourcePulseDbContext db,
    ICapacityQueryService capacity) : ILoadQueryService
{
    private const int MaxRangeDays = 366;

    public async Task<ServiceResult<IReadOnlyList<DailyLoadDto>>> GetForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default)
    {
        if (from > toInclusive)
        {
            return ServiceResult<IReadOnlyList<DailyLoadDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });
        }

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
        {
            return ServiceResult<IReadOnlyList<DailyLoadDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });
        }

        var resourceExists = await db.Resources.AnyAsync(r => r.Id == resourceId, ct);
        if (!resourceExists)
            return ServiceResult<IReadOnlyList<DailyLoadDto>>.NotFound($"Resource {resourceId} not found.");

        // Capacity first; if it fails, surface that result directly.
        var capacityResult = await capacity.GetForResourceAsync(resourceId, from, toInclusive, ct);
        if (capacityResult.IsFailure)
            return ServiceResult<IReadOnlyList<DailyLoadDto>>.Failure(capacityResult.Error!);

        var capacityByDate = capacityResult.Value.ToDictionary(d => d.Date, d => d.Hours);

        // Allocations overlapping [from, toInclusive] for this resource.
        var allocations = await db.Allocations
            .AsNoTracking()
            .Where(a => a.ResourceId == resourceId
                     && a.PeriodStart <= toInclusive
                     && a.PeriodEnd >= from)
            .ToListAsync(ct);

        var result = LoadCalculator
            .ForResourceAndRange(resourceId, allocations, capacityByDate, from, toInclusive)
            .Select(d => new DailyLoadDto { Date = d.Date, Hours = d.Hours, LoadPercent = d.LoadPercent })
            .ToList();

        return ServiceResult<IReadOnlyList<DailyLoadDto>>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<DailyNodeLoadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default)
    {
        if (from > toInclusive)
        {
            return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });
        }

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
        {
            return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });
        }

        var nodeMeta = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => new { p.Id, p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (nodeMeta is null)
            return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.NotFound($"ProjectNode {projectNodeId} not found.");

        if (nodeMeta.NodeType != ProjectNodeType.Project && nodeMeta.NodeType != ProjectNodeType.Phase)
            return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Validation(new Dictionary<string, string[]>
            {
                [nameof(projectNodeId)] = [$"ProjectNode {projectNodeId} is a {nodeMeta.NodeType}; load is only defined for Project and Phase nodes."]
            });

        // Subtree aggregation (ADR-0022): the node itself + every descendant via
        // the materialized-path prefix. Invariant I1 permits allocations on
        // Project/Phase nodes, so a project that staffs its Phases would otherwise
        // lose those blocks under an exact-node filter (gap #5 / D1).
        var subtreePrefix = nodeMeta.Path + "/";
        var allocations = await db.Allocations
            .AsNoTracking()
            .Where(a => db.ProjectNodes.Any(p => p.Id == a.ProjectNodeId
                          && (p.Id == projectNodeId || p.Path.StartsWith(subtreePrefix)))
                     && a.PeriodStart <= toInclusive
                     && a.PeriodEnd >= from)
            .ToListAsync(ct);

        // Placeholders carry no resource — skip them when collecting the
        // resource set for capacity lookups (ADR-0016 §5). Their rate%
        // contribution is computed by LoadCalculator into PlaceholderRatePercent.
        var resourceIds = allocations
            .Where(a => a.ResourceId is not null)
            .Select(a => a.ResourceId!.Value)
            .Distinct()
            .ToList();

        // Sequential capacity loads — the pooled DbContext is not safe for
        // concurrent queries (D4 in the Phase 4 plan).
        var capacityByResourceAndDate = new Dictionary<(Guid, DateOnly), TimeSpan>();
        foreach (var rid in resourceIds)
        {
            var capResult = await capacity.GetForResourceAsync(rid, from, toInclusive, ct);
            if (capResult.IsFailure)
                return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Failure(capResult.Error!);

            foreach (var d in capResult.Value)
                capacityByResourceAndDate[(rid, d.Date)] = d.Hours;
        }

        var resourceNames = await db.Resources
            .AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var dtos = LoadCalculator
            .ForProjectSubtreeAndRange(allocations, capacityByResourceAndDate, from, toInclusive)
            .Select(d => new DailyNodeLoadDto
            {
                Date = d.Date,
                TotalHours = d.TotalHours,
                ByResource = d.ByResource
                    .Select(kvp => new NodeLoadByResourceDto
                    {
                        ResourceId = kvp.Key,
                        ResourceName = resourceNames.GetValueOrDefault(kvp.Key, string.Empty),
                        Hours = kvp.Value
                    })
                    .OrderBy(x => x.ResourceName)
                    .ToList(),
                PlaceholderRatePercent = d.PlaceholderRatePercent
            })
            .ToList();

        return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Success(dtos);
    }
}
