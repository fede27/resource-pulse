using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.ExternalConstraints;

public sealed class ExternalConstraintService(
    IRepository<ExternalConstraint, Guid> repository,
    ResourcePulseDbContext db,
    IMapper mapper) : IExternalConstraintService
{
    public async Task<ServiceResult<IReadOnlyList<ExternalConstraintReadDto>>> GetForRootAsync(
        Guid rootProjectId, CancellationToken ct = default)
    {
        var rows = await db.ExternalConstraints.AsNoTracking()
            .Where(c => c.RootProjectId == rootProjectId)
            .OrderBy(c => c.Date).ThenBy(c => c.Name)
            .ToListAsync(ct);
        var counts = await AnchoredEdgeCountsAsync(rows.Select(r => r.Id).ToList(), ct);
        var dtos = rows.Select(r => ToDto(r, counts)).ToList();
        return ServiceResult<IReadOnlyList<ExternalConstraintReadDto>>.Success(dtos);
    }

    public async Task<ServiceResult<ExternalConstraintReadDto>> GetByIdAsync(
        Guid rootProjectId, Guid id, CancellationToken ct = default)
    {
        var row = await db.ExternalConstraints.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.RootProjectId == rootProjectId, ct);
        if (row is null) return NotFound(id);
        var counts = await AnchoredEdgeCountsAsync([id], ct);
        return ServiceResult<ExternalConstraintReadDto>.Success(ToDto(row, counts));
    }

    public async Task<ServiceResult<ExternalConstraintReadDto>> CreateAsync(
        Guid rootProjectId, CreateExternalConstraintDto dto, CancellationToken ct = default)
    {
        // An imposed date belongs to a ROOT project: the tender, the contract,
        // are signed for the project, not for one of its phases.
        var root = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootProjectId)
            .Select(p => new { p.NodeType })
            .FirstOrDefaultAsync(ct);
        if (root is null)
            return ServiceResult<ExternalConstraintReadDto>.NotFound($"ProjectNode {rootProjectId} not found.");
        if (root.NodeType != ProjectNodeType.Project)
            return ServiceResult<ExternalConstraintReadDto>.Validation(new Dictionary<string, string[]>
            {
                ["RootProjectId"] = [$"External constraints attach to a Project root (got {root.NodeType})."]
            });

        ExternalConstraint c;
        try { c = ExternalConstraint.Create(rootProjectId, dto.Name, dto.Date, dto.Authority, dto.Notes); }
        catch (DomainException ex) { return ServiceResult<ExternalConstraintReadDto>.Conflict(ex.Message); }

        await repository.AddAsync(c, ct);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<ExternalConstraintReadDto>.Success(ToDto(c, new Dictionary<Guid, int>()));
    }

    public async Task<ServiceResult<ExternalConstraintReadDto>> UpdateAsync(
        Guid rootProjectId, Guid id, UpdateExternalConstraintDto dto, CancellationToken ct = default)
    {
        var c = await db.ExternalConstraints
            .FirstOrDefaultAsync(x => x.Id == id && x.RootProjectId == rootProjectId, ct);
        if (c is null) return NotFound(id);

        // The date is a REFERENT (ADR-0034 §5): once boundaries follow it, it
        // moves through the envelope, where dryRun shows what moves with it.
        var count = 0;
        if (dto.Date != c.Date)
        {
            count = await AnchoredEdgeCountAsync(id, ct);
            if (count > 0)
                return ServiceResult<ExternalConstraintReadDto>.Conflict(
                    $"{count} coverage boundary(ies) are anchored to this constraint's date; cannot move it from here. " +
                    "Use the plan command 'moveConstraint' to move them with it, or pin them first.");
        }

        try
        {
            c.Rename(dto.Name);
            c.ChangeAuthority(dto.Authority);
            c.Annotate(dto.Notes);
            c.MoveTo(dto.Date);
        }
        catch (DomainException ex) { return ServiceResult<ExternalConstraintReadDto>.Conflict(ex.Message); }

        await repository.SaveChangesAsync(ct);
        var counts = await AnchoredEdgeCountsAsync([id], ct);
        return ServiceResult<ExternalConstraintReadDto>.Success(ToDto(c, counts));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid rootProjectId, Guid id, CancellationToken ct = default)
    {
        var c = await db.ExternalConstraints
            .FirstOrDefaultAsync(x => x.Id == id && x.RootProjectId == rootProjectId, ct);
        if (c is null) return ServiceResult.NotFound($"External constraint {id} not found.");

        // Restrict FK from allocations.*_anchor_constraint_id: count first so a
        // plain conflict does not surface as a fault. Pin the boundaries first.
        var anchored = await AnchoredEdgeCountAsync(id, ct);
        if (anchored > 0)
            return ServiceResult.Conflict(
                $"Cannot delete a constraint that {anchored} coverage boundary(ies) are anchored to. Pin them first.");

        c.MarkDeleted();
        repository.Remove(c);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            return ServiceResult.Conflict("Cannot delete a constraint that coverage boundaries are still anchored to.");
        }
        return ServiceResult.Ok();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private ExternalConstraintReadDto ToDto(ExternalConstraint c, IReadOnlyDictionary<Guid, int> counts)
    {
        var dto = mapper.Map<ExternalConstraintReadDto>(c);
        dto.AnchoredEdgeCount = counts.GetValueOrDefault(c.Id);
        return dto;
    }

    private Task<int> AnchoredEdgeCountAsync(Guid constraintId, CancellationToken ct) =>
        db.Allocations.AsNoTracking()
            .CountAsync(a => a.StartAnchor.ConstraintId == constraintId || a.EndAnchor.ConstraintId == constraintId, ct);

    private async Task<IReadOnlyDictionary<Guid, int>> AnchoredEdgeCountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        if (ids.Count == 0) return counts;

        var starts = await db.Allocations.AsNoTracking()
            .Where(a => a.StartAnchor.ConstraintId != null && ids.Contains(a.StartAnchor.ConstraintId.Value))
            .GroupBy(a => a.StartAnchor.ConstraintId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var ends = await db.Allocations.AsNoTracking()
            .Where(a => a.EndAnchor.ConstraintId != null && ids.Contains(a.EndAnchor.ConstraintId.Value))
            .GroupBy(a => a.EndAnchor.ConstraintId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var s in starts) counts[s.Key] = counts.GetValueOrDefault(s.Key) + s.Count;
        foreach (var e in ends) counts[e.Key] = counts.GetValueOrDefault(e.Key) + e.Count;
        return counts;
    }

    private static ServiceResult<ExternalConstraintReadDto> NotFound(Guid id) =>
        ServiceResult<ExternalConstraintReadDto>.NotFound($"External constraint {id} not found.");
}
