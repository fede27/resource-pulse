using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Allocations;

// Cross-aggregate invariants enforced here (see Phase 4 plan §2):
//   I1  ProjectNode is at capacity-planning level (Project or Phase).
//   I2  No-overlap on (ResourceId, ProjectNodeId) — service check + DB EXCLUDE.
//   I3  Resource is active.
//   I4  Root project is not Closed or Cancelled.
// I5 (cross-project overallocation) is permitted by design.
public sealed class AllocationService(
    IRepository<Allocation, Guid> repository,
    ResourcePulseDbContext db) : IAllocationService
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
        return dto is null
            ? ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.")
            : ServiceResult<AllocationReadDto>.Success(dto);
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

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<ServiceResult<AllocationReadDto>> CreateAsync(
        CreateAllocationDto dto, CancellationToken ct = default)
    {
        // I1: project node exists and is at capacity-planning level.
        var nodeInfo = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == dto.ProjectNodeId)
            .Select(p => new { p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (nodeInfo is null)
            return ServiceResult<AllocationReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(CreateAllocationDto.ProjectNodeId)] = [$"ProjectNode {dto.ProjectNodeId} does not exist."]
            });

        if (nodeInfo.NodeType != ProjectNodeType.Project && nodeInfo.NodeType != ProjectNodeType.Phase)
            return ServiceResult<AllocationReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(CreateAllocationDto.ProjectNodeId)] =
                    [$"Allocations are only allowed on Project or Phase nodes (got {nodeInfo.NodeType})."]
            });

        // I3: resource exists and is active.
        var resourceInfo = await db.Resources
            .AsNoTracking()
            .Where(r => r.Id == dto.ResourceId)
            .Select(r => new { r.IsActive })
            .FirstOrDefaultAsync(ct);

        if (resourceInfo is null)
            return ServiceResult<AllocationReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(CreateAllocationDto.ResourceId)] = [$"Resource {dto.ResourceId} does not exist."]
            });

        if (!resourceInfo.IsActive)
            return ServiceResult<AllocationReadDto>.Conflict(
                $"Resource {dto.ResourceId} is inactive and cannot be allocated.");

        // I4: root project status not terminal.
        var rootValidation = await ValidateProjectStatusAsync<AllocationReadDto>(nodeInfo.Path, ct);
        if (rootValidation is { } pv) return pv;

        // I2: no overlap on (Resource, ProjectNode).
        var overlap = await OverlapsAsync(
            dto.ResourceId, dto.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            excludeId: null, ct);
        if (overlap)
            return ServiceResult<AllocationReadDto>.Conflict(
                $"Resource {dto.ResourceId} already has an overlapping allocation on project node {dto.ProjectNodeId}.");

        Allocation allocation;
        try
        {
            allocation = Allocation.Create(
                dto.ResourceId, dto.ProjectNodeId,
                dto.PeriodStart, dto.PeriodEnd,
                dto.AllocationPercent, dto.Notes);
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
            // Race lost between the service-level check and the DB EXCLUDE constraint.
            return ServiceResult<AllocationReadDto>.Conflict(
                "Allocation overlaps an existing allocation on the same resource and project node.");
        }

        return await GetByIdAsync(allocation.Id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> UpdateAsync(
        Guid id, UpdateAllocationDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        // I2 with new dates, excluding self.
        var overlap = await OverlapsAsync(
            allocation.ResourceId, allocation.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            excludeId: id, ct);
        if (overlap)
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
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            CreatedBy = a.CreatedBy,
            UpdatedAt = a.UpdatedAt,
            UpdatedBy = a.UpdatedBy
        };

    private async Task<ServiceResult<T>?> ValidateProjectStatusAsync<T>(string nodePath, CancellationToken ct)
    {
        // Path is "/{rootId}/..." — first segment is the root id.
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

    // Inner pg exception text contains the constraint name on EXCLUDE violations.
    private static bool IsNoOverlapViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("allocations_no_overlap", StringComparison.OrdinalIgnoreCase) == true;

}
