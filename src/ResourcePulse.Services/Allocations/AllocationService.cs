using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;

namespace ResourcePulse.Services.Allocations;

// READ side only (ADR-0018). All plan mutation moved to PlanCommandService
// behind the command envelope. This service keeps the allocation queries and
// the ResolvedHours enrichment/sidecar.
public sealed class AllocationService(
    ResourcePulseDbContext db,
    ICapacityQueryService capacity) : IAllocationService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = BuildReadQuery();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<AllocationReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await BuildReadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dto is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        return ServiceResult<AllocationReadDto>.Success(await EnrichWithResolvedHoursAsync(dto, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        if (from > toInclusive)
            return RangeValidation<IReadOnlyList<AllocationReadDto>>();

        var list = await BuildReadQuery()
            .Where(x => x.ResourceId == resourceId
                     && x.PeriodStart <= toInclusive
                     && x.PeriodEnd >= from)
            .OrderBy(x => x.PeriodStart)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<AllocationReadDto>>.Success(list);
    }

    public async Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default)
    {
        if (from > toInclusive)
            return RangeValidation<IReadOnlyList<AllocationReadDto>>();

        // Subtree aggregation (ADR-0022): the node + every descendant via the
        // materialized-path prefix, not just the exact node. A project that staffs
        // its Phases would otherwise drop those blocks/holes from the aggregate
        // view (gap #5 / D1). BuildReadQuery already projects the allocation's
        // node Path, so we filter on it after projection.
        var nodePath = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);

        if (nodePath is null)
            return ServiceResult<IReadOnlyList<AllocationReadDto>>.Success(Array.Empty<AllocationReadDto>());

        var subtreePrefix = nodePath + "/";
        var list = await BuildReadQuery()
            .Where(x => (x.ProjectNodePath == nodePath || x.ProjectNodePath.StartsWith(subtreePrefix))
                     && x.PeriodStart <= toInclusive
                     && x.PeriodEnd >= from)
            .OrderBy(x => x.PeriodStart)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<AllocationReadDto>>.Success(list);
    }

    public async Task<ServiceResult<AllocationResolvedHoursDto>> GetResolvedHoursAsync(
        Guid id, CancellationToken ct = default)
    {
        var allocation = await db.Allocations
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.ResourceId, a.PeriodStart, a.PeriodEnd, a.AllocationPercent })
            .FirstOrDefaultAsync(ct);

        if (allocation is null)
            return ServiceResult<AllocationResolvedHoursDto>.NotFound($"Allocation {id} not found.");

        var capacityResult = await CapacityInWindowAsync(
            allocation.ResourceId, allocation.PeriodStart, allocation.PeriodEnd, ct);
        if (capacityResult.IsFailure)
            return ServiceResult<AllocationResolvedHoursDto>.Failure(capacityResult.Error!);

        var totalCapacity = capacityResult.Value;
        var resolvedHours = AllocationResolver.HoursForPercent(allocation.AllocationPercent, totalCapacity);

        return ServiceResult<AllocationResolvedHoursDto>.Success(new AllocationResolvedHoursDto
        {
            AllocationPercent = allocation.AllocationPercent,
            ResolvedHours = resolvedHours,
            CapacityInWindow = totalCapacity
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Read projection without ResolvedHours (list reads). Detail reads enrich
    // afterward via EnrichWithResolvedHoursAsync. See ADR-0013, D1.
    private IQueryable<AllocationReadDto> BuildReadQuery() =>
        from a in db.Allocations.AsNoTracking()
        join p in db.ProjectNodes.AsNoTracking() on a.ProjectNodeId equals p.Id
        join d in db.Demands.AsNoTracking() on a.DemandId equals d.Id
        join drole in db.Roles.AsNoTracking() on d.RoleId equals drole.Id
        from r in db.Resources.AsNoTracking()
            .Where(r => r.Id == a.ResourceId).DefaultIfEmpty()
        from prole in db.Roles.AsNoTracking()
            .Where(prole => r != null && prole.Id == r.RoleId).DefaultIfEmpty()
        select new AllocationReadDto
        {
            Id = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = r != null ? r.Name : null,
            ResourceRoleId = r != null ? r.RoleId : null,
            ResourceRoleName = prole != null ? prole.Name : null,
            DemandId = a.DemandId,
            DemandRoleId = d.RoleId,
            DemandRoleName = drole.Name,
            ProjectNodeId = a.ProjectNodeId,
            ProjectNodePath = p.Path,
            PeriodStart = a.PeriodStart,
            PeriodEnd = a.PeriodEnd,
            AllocationPercent = a.AllocationPercent,
            Status = a.Status,
            ResolvedHours = null,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            CreatedBy = a.CreatedBy,
            UpdatedAt = a.UpdatedAt,
            UpdatedBy = a.UpdatedBy
        };

    private async Task<AllocationReadDto> EnrichWithResolvedHoursAsync(
        AllocationReadDto dto, CancellationToken ct)
    {
        var capacityResult = await CapacityInWindowAsync(
            dto.ResourceId, dto.PeriodStart, dto.PeriodEnd, ct);
        if (capacityResult.IsFailure || capacityResult.Value <= TimeSpan.Zero)
            return dto;

        var hours = AllocationResolver.HoursForPercent(dto.AllocationPercent, capacityResult.Value);
        return new AllocationReadDto
        {
            Id = dto.Id,
            ResourceId = dto.ResourceId,
            ResourceName = dto.ResourceName,
            ResourceRoleId = dto.ResourceRoleId,
            ResourceRoleName = dto.ResourceRoleName,
            DemandId = dto.DemandId,
            DemandRoleId = dto.DemandRoleId,
            DemandRoleName = dto.DemandRoleName,
            ProjectNodeId = dto.ProjectNodeId,
            ProjectNodePath = dto.ProjectNodePath,
            PeriodStart = dto.PeriodStart,
            PeriodEnd = dto.PeriodEnd,
            AllocationPercent = dto.AllocationPercent,
            Status = dto.Status,
            ResolvedHours = hours,
            Notes = dto.Notes,
            CreatedAt = dto.CreatedAt,
            CreatedBy = dto.CreatedBy,
            UpdatedAt = dto.UpdatedAt,
            UpdatedBy = dto.UpdatedBy
        };
    }

    private async Task<ServiceResult<TimeSpan>> CapacityInWindowAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct)
    {
        var capacityResult = await capacity.GetForResourceAsync(resourceId, from, toInclusive, ct);
        if (capacityResult.IsFailure)
            return ServiceResult<TimeSpan>.Failure(capacityResult.Error!);

        var total = TimeSpan.Zero;
        foreach (var d in capacityResult.Value) total += d.Hours;
        return ServiceResult<TimeSpan>.Success(total);
    }

    private static ServiceResult<T> RangeValidation<T>() =>
        ServiceResult<T>.Validation(new Dictionary<string, string[]>
        {
            ["range"] = ["'from' must be on or before 'to'."]
        });
}
