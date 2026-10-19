using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using TimeSloth.Common.Domain;
using TimeSloth.Common.Results;
using TimeSloth.Domain;
using TimeSloth.Domain.Projects;
using TimeSloth.Persistence;

namespace TimeSloth.Services.ExternalConstraints;

public sealed class ExternalConstraintService(
    IRepository<ExternalConstraint, Guid> repository,
    TimeSlothDbContext db,
    IMapper mapper) : IExternalConstraintService
{
    public async Task<ServiceResult<IReadOnlyList<ExternalConstraintReadDto>>> GetForRootAsync(
        Guid rootProjectId, CancellationToken ct = default)
    {
        var rows = await db.ExternalConstraints.AsNoTracking()
            .Where(c => c.RootProjectId == rootProjectId)
            .OrderBy(c => c.Date).ThenBy(c => c.Name)
            .ToListAsync(ct);
        var dtos = rows.Select(mapper.Map<ExternalConstraintReadDto>).ToList();
        return ServiceResult<IReadOnlyList<ExternalConstraintReadDto>>.Success(dtos);
    }

    public async Task<ServiceResult<ExternalConstraintReadDto>> GetByIdAsync(
        Guid rootProjectId, Guid id, CancellationToken ct = default)
    {
        var row = await db.ExternalConstraints.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.RootProjectId == rootProjectId, ct);
        if (row is null) return NotFound(id);
        return ServiceResult<ExternalConstraintReadDto>.Success(mapper.Map<ExternalConstraintReadDto>(row));
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
        catch (DomainException ex) { return ServiceResult<ExternalConstraintReadDto>.Conflict(ex); }

        await repository.AddAsync(c, ct);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<ExternalConstraintReadDto>.Success(mapper.Map<ExternalConstraintReadDto>(c));
    }

    public async Task<ServiceResult<ExternalConstraintReadDto>> UpdateAsync(
        Guid rootProjectId, Guid id, UpdateExternalConstraintDto dto, CancellationToken ct = default)
    {
        var c = await db.ExternalConstraints
            .FirstOrDefaultAsync(x => x.Id == id && x.RootProjectId == rootProjectId, ct);
        if (c is null) return NotFound(id);

        // An imposed date is NOT a referent (ADR-0035 §4). The plan does not
        // promise to chase a date that lives outside it: moving this one moves no
        // block, and the value that survives is the DELTA between it and what the
        // plan actually sustains — the negotiation surface. So this is an
        // ordinary edit, never refused.
        try
        {
            c.Rename(dto.Name);
            c.ChangeAuthority(dto.Authority);
            c.Annotate(dto.Notes);
            c.MoveTo(dto.Date);
        }
        catch (DomainException ex) { return ServiceResult<ExternalConstraintReadDto>.Conflict(ex); }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ExternalConstraintReadDto>.Success(mapper.Map<ExternalConstraintReadDto>(c));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid rootProjectId, Guid id, CancellationToken ct = default)
    {
        var c = await db.ExternalConstraints
            .FirstOrDefaultAsync(x => x.Id == id && x.RootProjectId == rootProjectId, ct);
        if (c is null) return ServiceResult.NotFound($"External constraint {id} not found.");

        // Nothing references a constraint any more (ADR-0035 dropped the FK from
        // allocations), so a delete is a delete.
        c.MarkDeleted();
        repository.Remove(c);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static ServiceResult<ExternalConstraintReadDto> NotFound(Guid id) =>
        ServiceResult<ExternalConstraintReadDto>.NotFound($"External constraint {id} not found.");
}
