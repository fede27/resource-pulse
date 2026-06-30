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

        var list = await BuildReadQuery()
            .Where(x => x.ProjectNodeId == projectNodeId
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

        if (allocation.ResourceId is null)
            return ServiceResult<AllocationResolvedHoursDto>.Conflict(
                "Allocation is a placeholder (no resource): resolved hours are not defined without a capacity reference.");

        var capacityResult = await CapacityInWindowAsync(
            allocation.ResourceId.Value, allocation.PeriodStart, allocation.PeriodEnd, ct);
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
        from r in db.Resources.AsNoTracking()
            .Where(r => r.Id == a.ResourceId).DefaultIfEmpty()
        from role in db.Roles.AsNoTracking()
            .Where(role => role.Id == a.RoleId).DefaultIfEmpty()
        from o in db.Resources.AsNoTracking()
            .Where(o => o.Id == a.OwnerResourceId).DefaultIfEmpty()
        select new AllocationReadDto
        {
            Id = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = r != null ? r.Name : null,
            ProjectNodeId = a.ProjectNodeId,
            ProjectNodePath = p.Path,
            PeriodStart = a.PeriodStart,
            PeriodEnd = a.PeriodEnd,
            AllocationPercent = a.AllocationPercent,
            Status = a.Status,
            RoleId = a.RoleId,
            RoleName = role != null ? role.Name : null,
            OwnerResourceId = a.OwnerResourceId,
            OwnerResourceName = o != null ? o.Name : null,
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
        // Placeholders have no resource — leave ResolvedHours null (ADR-0016).
        if (dto.ResourceId is null) return dto;

        var capacityResult = await CapacityInWindowAsync(
            dto.ResourceId.Value, dto.PeriodStart, dto.PeriodEnd, ct);
        if (capacityResult.IsFailure || capacityResult.Value <= TimeSpan.Zero)
            return dto;

        var hours = AllocationResolver.HoursForPercent(dto.AllocationPercent, capacityResult.Value);
        return new AllocationReadDto
        {
            Id = dto.Id,
            ResourceId = dto.ResourceId,
            ResourceName = dto.ResourceName,
            ProjectNodeId = dto.ProjectNodeId,
            ProjectNodePath = dto.ProjectNodePath,
            PeriodStart = dto.PeriodStart,
            PeriodEnd = dto.PeriodEnd,
            AllocationPercent = dto.AllocationPercent,
            Status = dto.Status,
            RoleId = dto.RoleId,
            RoleName = dto.RoleName,
            OwnerResourceId = dto.OwnerResourceId,
            OwnerResourceName = dto.OwnerResourceName,
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
