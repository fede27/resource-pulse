using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Demands;

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
        AllocationStatus? status = null,
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

        // Allocations overlapping [from, toInclusive] for this resource. The
        // optional status filter narrows to one commitment status before the pure
        // calculator runs (calculator stays status-agnostic, ADR-0010) — same
        // pattern as the commitment-profile read below.
        var allocations = await db.Allocations
            .AsNoTracking()
            .Where(a => a.ResourceId == resourceId
                     && (status == null || a.Status == status)
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

        // Every allocation is a coverage with a resource (Phase 5.1, ADR-0025).
        var resourceIds = allocations
            .Select(a => a.ResourceId)
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
                    .ToList()
            })
            .ToList();

        return ServiceResult<IReadOnlyList<DailyNodeLoadDto>>.Success(dtos);
    }

    public async Task<ServiceResult<IReadOnlyList<LoadSegmentDto>>> GetCommitmentProfileForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        AllocationStatus? status = null,
        CancellationToken ct = default)
    {
        if (from > toInclusive)
            return ServiceResult<IReadOnlyList<LoadSegmentDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
            return ServiceResult<IReadOnlyList<LoadSegmentDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });

        var resourceExists = await db.Resources.AnyAsync(r => r.Id == resourceId, ct);
        if (!resourceExists)
            return ServiceResult<IReadOnlyList<LoadSegmentDto>>.NotFound($"Resource {resourceId} not found.");

        // Assigned allocations of this resource overlapping the horizon. Placeholders
        // have no ResourceId, so they are excluded by construction (ADR-0016 §5).
        // The optional status filter narrows to one commitment status before the
        // pure calculator runs (calculator stays status-agnostic, ADR-0010).
        var allocations = await db.Allocations
            .AsNoTracking()
            .Where(a => a.ResourceId == resourceId
                     && (status == null || a.Status == status)
                     && a.PeriodStart <= toInclusive
                     && a.PeriodEnd >= from)
            .ToListAsync(ct);

        // Map each allocation's node to its ROOT project node id (first segment of
        // the materialized Path), so the profile decomposes by project.
        var nodeIds = allocations.Select(a => a.ProjectNodeId).Distinct().ToList();
        var nodePaths = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => nodeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Path })
            .ToListAsync(ct);

        var rootByNode = nodePaths.ToDictionary(p => p.Id, p => RootIdFromPath(p.Path));

        var segments = LoadCalculator.ResourceCommitmentProfile(
            resourceId, allocations, rootByNode, from, toInclusive);

        // Resolve root project names for the breakdown (cheap, gap #7).
        var rootIds = rootByNode.Values.Distinct().ToList();
        var rootNames = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => rootIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var dtos = segments
            .Select(s => new LoadSegmentDto
            {
                From = s.From,
                To = s.To,
                Percent = s.Percent,
                ByProject = s.ByProject
                    .Select(kvp => new LoadSegmentProjectDto
                    {
                        ProjectNodeId = kvp.Key,
                        ProjectName = rootNames.GetValueOrDefault(kvp.Key, string.Empty),
                        Percent = kvp.Value
                    })
                    .OrderByDescending(x => x.Percent)
                    .ThenBy(x => x.ProjectName)
                    .ToList()
            })
            .ToList();

        return ServiceResult<IReadOnlyList<LoadSegmentDto>>.Success(dtos);
    }

    // ── Demand coverage (Phase 5.2, ADR-0025/0026) ───────────────────────────

    public async Task<ServiceResult<IReadOnlyList<DemandCoverageDto>>> GetDemandCoverageForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        if (from > toInclusive)
            return ServiceResult<IReadOnlyList<DemandCoverageDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });

        var nodePath = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == projectNodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (nodePath is null)
            return ServiceResult<IReadOnlyList<DemandCoverageDto>>.NotFound($"ProjectNode {projectNodeId} not found.");

        var prefix = nodePath + "/";
        var demands = await db.Demands.AsNoTracking()
            .Where(d => db.ProjectNodes.Any(p => p.Id == d.ProjectNodeId
                        && (p.Id == projectNodeId || p.Path.StartsWith(prefix))))
            .ToListAsync(ct);

        var dtos = await ReconcileAsync(demands, from, toInclusive, ct);
        return ServiceResult<IReadOnlyList<DemandCoverageDto>>.Success(dtos);
    }

    public async Task<ServiceResult<DemandCoverageDto>> GetDemandCoverageForDemandAsync(
        Guid demandId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        if (from > toInclusive)
            return ServiceResult<DemandCoverageDto>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });

        var demand = await db.Demands.AsNoTracking().FirstOrDefaultAsync(d => d.Id == demandId, ct);
        if (demand is null)
            return ServiceResult<DemandCoverageDto>.NotFound($"Demand {demandId} not found.");

        var dtos = await ReconcileAsync([demand], from, toInclusive, ct);
        return ServiceResult<DemandCoverageDto>.Success(dtos[0]);
    }

    public async Task<ServiceResult<IReadOnlyList<OpenDemandDto>>> GetOpenDemandsAsync(
        Guid? roleId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        if (from > toInclusive)
            return ServiceResult<IReadOnlyList<OpenDemandDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
            return ServiceResult<IReadOnlyList<OpenDemandDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });

        if (roleId is Guid rid)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.Id == rid, ct);
            if (!roleExists)
                return ServiceResult<IReadOnlyList<OpenDemandDto>>.NotFound($"Role {rid} not found.");
        }

        // Candidate demands (optionally narrowed to the requested role) with their
        // node's materialized Path, so the root project is derivable in memory.
        var candidates = await db.Demands.AsNoTracking()
            .Where(d => roleId == null || d.RoleId == roleId)
            .Join(
                db.ProjectNodes.AsNoTracking(),
                d => d.ProjectNodeId,
                p => p.Id,
                (d, p) => new { Demand = d, p.Path })
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return ServiceResult<IReadOnlyList<OpenDemandDto>>.Success([]);

        var rootByDemand = candidates.ToDictionary(x => x.Demand.Id, x => RootIdFromPath(x.Path));

        // Drop demands whose root project is Closed/Cancelled — I4 forbids creating
        // coverage there, so offering them as targets would be a dead end.
        var rootIds = rootByDemand.Values.Distinct().ToList();
        var roots = await db.ProjectNodes.AsNoTracking()
            .Where(p => rootIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Status })
            .ToListAsync(ct);
        var rootById = roots.ToDictionary(r => r.Id);

        var demands = candidates
            .Select(x => x.Demand)
            .Where(d => rootById.TryGetValue(rootByDemand[d.Id], out var root)
                     && root.Status is not (ProjectStatus.Closed or ProjectStatus.Cancelled))
            .ToList();

        var reconciled = await ReconcileAsync(demands, from, toInclusive, ct);

        // Open = a concrete residual remains, or best-effort (no target ⇒ the demand
        // can always absorb coverage; §7 — null gap is "no defined gap", not zero).
        var demandById = demands.ToDictionary(d => d.Id);
        var dtos = reconciled
            .Where(c => c.IsBestEffort || c.GapHours > TimeSpan.Zero)
            .Select(c =>
            {
                var rootId = rootByDemand[c.DemandId];
                return new OpenDemandDto
                {
                    DemandId = c.DemandId,
                    ProjectNodeId = c.ProjectNodeId,
                    RootProjectId = rootId,
                    RootProjectName = rootById[rootId].Name,
                    RoleId = c.RoleId,
                    RoleName = c.RoleName,
                    Provenance = c.Provenance,
                    RequiredHours = c.RequiredHours,
                    CoveredHours = c.CoveredHours,
                    GapHours = c.GapHours,
                    OwnerResourceId = c.OwnerResourceId,
                    OwnerResourceName = c.OwnerResourceName,
                    Notes = demandById[c.DemandId].Notes
                };
            })
            // Concrete residuals first (largest gap on top); best-effort tail.
            .OrderByDescending(x => x.GapHours ?? TimeSpan.MinValue)
            .ThenBy(x => x.RootProjectName)
            .ThenBy(x => x.RoleName)
            .ToList();

        return ServiceResult<IReadOnlyList<OpenDemandDto>>.Success(dtos);
    }

    // Shared: load the coverage of the given demands + the capacity of the covering
    // resources, run the pure calculator, resolve role/owner names. Capacity loads
    // are sequential (pooled DbContext, ADR-0010).
    private async Task<IReadOnlyList<DemandCoverageDto>> ReconcileAsync(
        List<Demand> demands, DateOnly from, DateOnly toInclusive, CancellationToken ct)
    {
        if (demands.Count == 0) return [];

        var demandIds = demands.Select(d => d.Id).ToList();
        var coverage = await db.Allocations.AsNoTracking()
            .Where(a => demandIds.Contains(a.DemandId)
                     && a.PeriodStart <= toInclusive
                     && a.PeriodEnd >= from)
            .ToListAsync(ct);

        var capacityByResourceAndDate = new Dictionary<(Guid, DateOnly), TimeSpan>();
        foreach (var rid in coverage.Select(a => a.ResourceId).Distinct())
        {
            var cap = await capacity.GetForResourceAsync(rid, from, toInclusive, ct);
            if (cap.IsFailure) continue; // treat as zero capacity for that resource
            foreach (var d in cap.Value) capacityByResourceAndDate[(rid, d.Date)] = d.Hours;
        }

        var reconciled = LoadCalculator.CoverageForDemands(demands, coverage, capacityByResourceAndDate, from, toInclusive);

        var roleIds = demands.Select(d => d.RoleId).Distinct().ToList();
        var roleNames = await db.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var ownerIds = demands.Where(d => d.OwnerResourceId != null).Select(d => d.OwnerResourceId!.Value).Distinct().ToList();
        var ownerNames = await db.Resources.AsNoTracking()
            .Where(r => ownerIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var byId = demands.ToDictionary(d => d.Id);
        return reconciled.Select(c =>
        {
            var d = byId[c.DemandId];
            return new DemandCoverageDto
            {
                DemandId = c.DemandId,
                ProjectNodeId = c.ProjectNodeId,
                RoleId = c.RoleId,
                RoleName = roleNames.GetValueOrDefault(c.RoleId, string.Empty),
                Provenance = d.Provenance,
                RequiredHours = c.RequiredHours,
                CoveredHours = c.CoveredHours,
                GapHours = c.GapHours,
                OwnerResourceId = d.OwnerResourceId,
                OwnerResourceName = d.OwnerResourceId is Guid o ? ownerNames.GetValueOrDefault(o) : null
            };
        }).ToList();
    }

    // Root project node id = first segment of the materialized Path "/{rootId}/...".
    private static Guid RootIdFromPath(string path)
    {
        var first = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
        return Guid.Parse(first);
    }
}
