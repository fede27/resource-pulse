using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;

namespace ResourcePulse.Services.Allocations;

// Cross-aggregate invariants enforced here (see Phase 4 plan §2):
//   I1  ProjectNode is at capacity-planning level (Project or Phase).
//   I2  No-overlap on (ResourceId, ProjectNodeId) — service check + DB EXCLUDE.
//   I3  Resource is active.
//   I4  Root project is not Closed or Cancelled.
// I5 (cross-project + single-allocation overallocation up to 1000%) is
// permitted by design — see ADR-0011 and ADR-0013.
//
// Vocabularies (Phase 4.1):
//   CreateByPercentAsync — rate-shaped input. Percent is stored as given.
//   CreateByHoursAsync   — quantity-shaped input. Resolves to percent via
//                          AllocationResolver using window capacity.
//   MoveAsync            — change the window; user picks which dimension to
//                          preserve (KeepPercent / KeepHours).
public sealed class AllocationService(
    IRepository<Allocation, Guid> repository,
    ResourcePulseDbContext db,
    ICapacityQueryService capacity) : IAllocationService
{
    // ── Reads ───────────────────────────────────────────────────────────────

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

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<ServiceResult<AllocationReadDto>> CreateByPercentAsync(
        CreateByPercentDto dto, CancellationToken ct = default)
    {
        var pre = await ValidateCreatePreconditionsAsync(
            dto.ResourceId, dto.ProjectNodeId, dto.PeriodStart, dto.PeriodEnd, ct);
        if (pre.IsFailure)
            return ServiceResult<AllocationReadDto>.Failure(pre.Error!);

        return await PersistNewAsync(
            dto.ResourceId, dto.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            dto.Percent, dto.Notes, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> CreateByHoursAsync(
        CreateByHoursDto dto, CancellationToken ct = default)
    {
        var pre = await ValidateCreatePreconditionsAsync(
            dto.ResourceId, dto.ProjectNodeId, dto.PeriodStart, dto.PeriodEnd, ct);
        if (pre.IsFailure)
            return ServiceResult<AllocationReadDto>.Failure(pre.Error!);

        var capacityResult = await CapacityInWindowAsync(
            dto.ResourceId, dto.PeriodStart, dto.PeriodEnd, ct);
        if (capacityResult.IsFailure)
            return ServiceResult<AllocationReadDto>.Failure(capacityResult.Error!);

        if (capacityResult.Value <= TimeSpan.Zero)
            return ServiceResult<AllocationReadDto>.Conflict(
                "Cannot allocate hours: resource has zero capacity in the requested window.");

        decimal percent;
        try
        {
            percent = AllocationResolver.PercentForHours(dto.TargetHours, capacityResult.Value);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        if (percent > Allocation.MaxAllocationPercent)
            return ServiceResult<AllocationReadDto>.Conflict(
                $"Resolved percent {percent} exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                "Widen the window or reduce target hours.");

        return await PersistNewAsync(
            dto.ResourceId, dto.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            percent, dto.Notes, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> UpdateAsync(
        Guid id, UpdateAllocationDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        // I2 with new dates, excluding self.
        if (await OverlapsAsync(allocation.ResourceId, allocation.ProjectNodeId,
                dto.PeriodStart, dto.PeriodEnd, excludeId: id, ct))
            return ServiceResult<AllocationReadDto>.Conflict(
                "Updated period overlaps another allocation on the same resource and project node.");

        try
        {
            allocation.ChangePeriod(dto.PeriodStart, dto.PeriodEnd);
            allocation.ChangePercent(dto.AllocationPercent);
            allocation.Annotate(dto.Notes);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNoOverlapViolation(ex))
        {
            return ServiceResult<AllocationReadDto>.Conflict(
                "Updated period overlaps another allocation on the same resource and project node.");
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> MoveAsync(
        Guid id, MoveAllocationDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        // I2 against the new window first — cheaper to bail than to compute
        // capacity and resolver math we'll throw away.
        if (await OverlapsAsync(allocation.ResourceId, allocation.ProjectNodeId,
                dto.NewPeriodStart, dto.NewPeriodEnd, excludeId: id, ct))
            return ServiceResult<AllocationReadDto>.Conflict(
                "Moved period overlaps another allocation on the same resource and project node.");

        // I4 may have shifted if the root project's status changed; re-check.
        var nodePath = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == allocation.ProjectNodeId)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);
        if (nodePath is not null)
        {
            var statusCheck = await ValidateProjectStatusAsync<AllocationReadDto>(nodePath, ct);
            if (statusCheck is { } sc) return sc;
        }

        decimal newPercent;
        switch (dto.Mode)
        {
            case MoveMode.KeepPercent:
                newPercent = allocation.AllocationPercent;
                break;

            case MoveMode.KeepHours:
            {
                var oldCapacity = await CapacityInWindowAsync(
                    allocation.ResourceId, allocation.PeriodStart, allocation.PeriodEnd, ct);
                if (oldCapacity.IsFailure)
                    return ServiceResult<AllocationReadDto>.Failure(oldCapacity.Error!);

                var newCapacity = await CapacityInWindowAsync(
                    allocation.ResourceId, dto.NewPeriodStart, dto.NewPeriodEnd, ct);
                if (newCapacity.IsFailure)
                    return ServiceResult<AllocationReadDto>.Failure(newCapacity.Error!);

                if (newCapacity.Value <= TimeSpan.Zero)
                    return ServiceResult<AllocationReadDto>.Conflict(
                        "Cannot move with KeepHours: resource has zero capacity in the new window.");

                // Hours implied by current allocation at its current capacity.
                // Note: if the *old* capacity is zero (calendar changed since
                // creation), HoursForPercent returns 0 and we'd resolve back to
                // an arbitrarily small percent. Treat that as a misconfiguration
                // and ask the caller to pick KeepPercent or fix the calendar.
                if (oldCapacity.Value <= TimeSpan.Zero)
                    return ServiceResult<AllocationReadDto>.Conflict(
                        "Cannot move with KeepHours: the original window now has zero capacity " +
                        "(calendar changed since the allocation was created). " +
                        "Use KeepPercent or correct the calendar first.");

                var oldHours = AllocationResolver.HoursForPercent(
                    allocation.AllocationPercent, oldCapacity.Value);

                try
                {
                    newPercent = AllocationResolver.PercentForHours(oldHours, newCapacity.Value);
                }
                catch (DomainException ex)
                {
                    return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
                }

                if (newPercent > Allocation.MaxAllocationPercent)
                    return ServiceResult<AllocationReadDto>.Conflict(
                        $"KeepHours move would require {newPercent}% in the new window, " +
                        $"which exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                        "Widen the new window or accept a different rate (use KeepPercent).");
                break;
            }

            default:
                return ServiceResult<AllocationReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(MoveAllocationDto.Mode)] = [$"Unknown move mode {dto.Mode}."]
                });
        }

        try
        {
            allocation.ChangePeriod(dto.NewPeriodStart, dto.NewPeriodEnd);
            allocation.ChangePercent(newPercent);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNoOverlapViolation(ex))
        {
            return ServiceResult<AllocationReadDto>.Conflict(
                "Moved period overlaps another allocation on the same resource and project node.");
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null) return ServiceResult.NotFound($"Allocation {id} not found.");

        allocation.MarkDeleted();
        repository.Remove(allocation);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Read projection without ResolvedHours (list reads). Detail reads enrich
    // afterward via EnrichWithResolvedHoursAsync. See ADR-0013, D1.
    private IQueryable<AllocationReadDto> BuildReadQuery() =>
        from a in db.Allocations.AsNoTracking()
        join r in db.Resources.AsNoTracking() on a.ResourceId equals r.Id
        join p in db.ProjectNodes.AsNoTracking() on a.ProjectNodeId equals p.Id
        select new AllocationReadDto
        {
            Id = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = r.Name,
            ProjectNodeId = a.ProjectNodeId,
            ProjectNodePath = p.Path,
            PeriodStart = a.PeriodStart,
            PeriodEnd = a.PeriodEnd,
            AllocationPercent = a.AllocationPercent,
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
        // If capacity lookup fails for any reason, we still return the DTO
        // without ResolvedHours rather than failing the whole read — the rest
        // of the data is correct, and the sidecar endpoint exists for retry.
        // If capacity is unavailable or zero, leave ResolvedHours null (dto
        // already carries null from BuildReadQuery) and return as-is.
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

    // Runs the four cross-aggregate gates (I1, I2, I3, I4) common to both
    // create paths. Returns a Unit success if all pass.
    private async Task<ServiceResult<Unit>> ValidateCreatePreconditionsAsync(
        Guid resourceId, Guid projectNodeId,
        DateOnly periodStart, DateOnly periodEnd,
        CancellationToken ct)
    {
        // I1
        var nodeInfo = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => new { p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (nodeInfo is null)
            return ServiceResult.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"ProjectNode {projectNodeId} does not exist."]
            });

        if (nodeInfo.NodeType != ProjectNodeType.Project && nodeInfo.NodeType != ProjectNodeType.Phase)
            return ServiceResult.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] =
                    [$"Allocations are only allowed on Project or Phase nodes (got {nodeInfo.NodeType})."]
            });

        // I3
        var resourceInfo = await db.Resources
            .AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new { r.IsActive })
            .FirstOrDefaultAsync(ct);

        if (resourceInfo is null)
            return ServiceResult.Validation(new Dictionary<string, string[]>
            {
                ["ResourceId"] = [$"Resource {resourceId} does not exist."]
            });

        if (!resourceInfo.IsActive)
            return ServiceResult.Conflict($"Resource {resourceId} is inactive and cannot be allocated.");

        // I4
        var statusCheck = await ValidateProjectStatusAsync<Unit>(nodeInfo.Path, ct);
        if (statusCheck is { } sc) return sc;

        // I2
        if (await OverlapsAsync(resourceId, projectNodeId, periodStart, periodEnd, excludeId: null, ct))
            return ServiceResult.Conflict(
                $"Resource {resourceId} already has an overlapping allocation on project node {projectNodeId}.");

        return ServiceResult.Ok();
    }

    private async Task<ServiceResult<AllocationReadDto>> PersistNewAsync(
        Guid resourceId, Guid projectNodeId,
        DateOnly periodStart, DateOnly periodEnd,
        decimal percent, string? notes,
        CancellationToken ct)
    {
        Allocation allocation;
        try
        {
            allocation = Allocation.Create(resourceId, projectNodeId, periodStart, periodEnd, percent, notes);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.AddAsync(allocation, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNoOverlapViolation(ex))
        {
            // Race lost between service-level check and DB EXCLUDE constraint.
            return ServiceResult<AllocationReadDto>.Conflict(
                "Allocation overlaps an existing allocation on the same resource and project node.");
        }

        return await GetByIdAsync(allocation.Id, ct);
    }

    private async Task<ServiceResult<T>?> ValidateProjectStatusAsync<T>(string nodePath, CancellationToken ct)
    {
        var rootIdString = nodePath.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdString, out var rootId))
            return ServiceResult<T>.Failure(ServiceError.Failure("ProjectNode has an invalid materialized path."));

        var status = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == rootId)
            .Select(p => p.Status)
            .FirstOrDefaultAsync(ct);

        if (status == ProjectStatus.Closed || status == ProjectStatus.Cancelled)
            return ServiceResult<T>.Conflict(
                $"Project root is {status} and cannot accept new or modified allocations.");

        return null;
    }

    private async Task<bool> OverlapsAsync(
        Guid resourceId, Guid projectNodeId,
        DateOnly newStart, DateOnly newEnd,
        Guid? excludeId, CancellationToken ct) =>
        await db.Allocations
            .AsNoTracking()
            .Where(a => a.ResourceId == resourceId
                     && a.ProjectNodeId == projectNodeId
                     && a.PeriodStart <= newEnd
                     && a.PeriodEnd >= newStart
                     && (excludeId == null || a.Id != excludeId))
            .AnyAsync(ct);

    private static ServiceResult<T> RangeValidation<T>() =>
        ServiceResult<T>.Validation(new Dictionary<string, string[]>
        {
            ["range"] = ["'from' must be on or before 'to'."]
        });

    private static bool IsNoOverlapViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("allocations_no_overlap", StringComparison.OrdinalIgnoreCase) == true;
}
