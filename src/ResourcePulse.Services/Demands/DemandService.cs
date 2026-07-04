using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Demands;

// READ side only (Phase 5.0, ADR-0018). Resolves FK→name (role, owner) and the
// node path in a single projection, mirroring AllocationService.BuildReadQuery.
public sealed class DemandService(ResourcePulseDbContext db) : IDemandService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default)
    {
        var result = await DataSourceLoader.LoadAsync(
            BuildReadQuery(), loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<DemandReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await BuildReadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return dto is null
            ? ServiceResult<DemandReadDto>.NotFound($"Demand {id} not found.")
            : ServiceResult<DemandReadDto>.Success(dto);
    }

    public async Task<ServiceResult<IReadOnlyList<DemandReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, CancellationToken ct = default)
    {
        var nodePath = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);

        if (nodePath is null)
            return ServiceResult<IReadOnlyList<DemandReadDto>>.Success(Array.Empty<DemandReadDto>());

        var subtreePrefix = nodePath + "/";
        var list = await BuildReadQuery()
            .Where(x => x.ProjectNodePath == nodePath || x.ProjectNodePath.StartsWith(subtreePrefix))
            .OrderBy(x => x.RoleName)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<DemandReadDto>>.Success(list);
    }

    private IQueryable<DemandReadDto> BuildReadQuery() =>
        from d in db.Demands.AsNoTracking()
        join p in db.ProjectNodes.AsNoTracking() on d.ProjectNodeId equals p.Id
        join role in db.Roles.AsNoTracking() on d.RoleId equals role.Id
        from o in db.Resources.AsNoTracking()
            .Where(o => o.Id == d.OwnerResourceId).DefaultIfEmpty()
        select new DemandReadDto
        {
            Id = d.Id,
            ProjectNodeId = d.ProjectNodeId,
            ProjectNodePath = p.Path,
            RoleId = d.RoleId,
            RoleName = role.Name,
            RequiredHours = d.RequiredHours,
            Provenance = d.Provenance,
            OwnerResourceId = d.OwnerResourceId,
            OwnerResourceName = o != null ? o.Name : null,
            Notes = d.Notes,
            CreatedAt = d.CreatedAt,
            CreatedBy = d.CreatedBy,
            UpdatedAt = d.UpdatedAt,
            UpdatedBy = d.UpdatedBy
        };
}
