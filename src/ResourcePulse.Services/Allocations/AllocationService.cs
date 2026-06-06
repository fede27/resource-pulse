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

// Cross-aggregate invariants enforced here:
//   I1  ProjectNode is at capacity-planning level (Project or Phase).
//   I3  Resource is active — VALUTATA SOLO PER LO STATO ASSEGNATO (ADR-0016).
//       I placeholder non hanno risorsa per definizione, il check non si applica.
//   I4  Root project is not Closed or Cancelled.
//   I6  Status = Hard è ammesso solo se il Project radice del nodo ha
//       CommitmentLevel ∈ {Committed, Critical} (ADR-0015). Si applica sia ai
//       blocchi assegnati sia ai placeholder.
//
// I2 (no-overlap on (ResourceId, ProjectNodeId)) is removed: overlapping
// blocks on the same (resource, project_node) are first-class and their rate%
// sums — see ADR-0014.
// I5 (cross-project + single-allocation overallocation up to 1000%) is
// permitted by design — see ADR-0011 and ADR-0013.
//
// Vocabularies (Phase 4.1):
//   CreateByPercentAsync — rate-shaped input. Percent is stored as given.
//   CreateByHoursAsync   — quantity-shaped input. Resolves to percent via
//                          AllocationResolver using window capacity.
//   MoveAsync            — change the window; user picks which dimension to
//                          preserve (KeepPercent / KeepHours).
//
// Placeholder operations (ADR-0016):
//   CreatePlaceholderByPercentAsync — crea direttamente un ruolo scoperto.
//   ConvertToPlaceholderAsync       — assegnato → placeholder.
//   AssignToResourceAsync           — placeholder → assegnato.
//
// Status (ADR-0015):
//   ChangeStatusAsync — promozione/demozione esplicita (Tentative ↔ Hard).
public sealed class AllocationService(
    IRepository<Allocation, Guid> repository,
    ResourcePulseDbContext db,
    ICapacityQueryService capacity) : IAllocationService
{
    // Mappatura "committato hard" — leggibile in un solo posto (ADR-0015 §3).
    // Cambiare la soglia significa cambiare solo qui.
    private static bool IsHardCommittedLevel(CommitmentLevel? level) =>
        level is CommitmentLevel.Committed or CommitmentLevel.Critical;

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

    // ── Writes — assigned creation ──────────────────────────────────────────

    public async Task<ServiceResult<AllocationReadDto>> CreateByPercentAsync(
        CreateByPercentDto dto, CancellationToken ct = default)
    {
        var pre = await ValidateAssignedCreatePreconditionsAsync(
            dto.ResourceId, dto.ProjectNodeId, dto.Status, ct);
        if (pre.IsFailure)
            return ServiceResult<AllocationReadDto>.Failure(pre.Error!);

        return await PersistNewAssignedAsync(
            dto.ResourceId, dto.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            dto.Percent, dto.Status, dto.Notes, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> CreateByHoursAsync(
        CreateByHoursDto dto, CancellationToken ct = default)
    {
        var pre = await ValidateAssignedCreatePreconditionsAsync(
            dto.ResourceId, dto.ProjectNodeId, dto.Status, ct);
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

        return await PersistNewAssignedAsync(
            dto.ResourceId, dto.ProjectNodeId,
            dto.PeriodStart, dto.PeriodEnd,
            percent, dto.Status, dto.Notes, ct);
    }

    // ── Writes — placeholder creation ───────────────────────────────────────

    public async Task<ServiceResult<AllocationReadDto>> CreatePlaceholderByPercentAsync(
        CreatePlaceholderByPercentDto dto, CancellationToken ct = default)
    {
        // I1 + I4 (apply to placeholders too) + I6.
        var pre = await ValidatePlaceholderCreatePreconditionsAsync(
            dto.ProjectNodeId, dto.RoleSkillId, dto.OwnerResourceId, dto.Status, ct);
        if (pre.IsFailure)
            return ServiceResult<AllocationReadDto>.Failure(pre.Error!);

        Allocation allocation;
        try
        {
            allocation = Allocation.CreatePlaceholder(
                dto.ProjectNodeId, dto.PeriodStart, dto.PeriodEnd,
                dto.Percent, dto.RoleSkillId, dto.OwnerResourceId,
                dto.Notes, dto.Status);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.AddAsync(allocation, ct);
        await repository.SaveChangesAsync(ct);

        return await GetByIdAsync(allocation.Id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> UpdateAsync(
        Guid id, UpdateAllocationDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

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

        await repository.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> MoveAsync(
        Guid id, MoveAllocationDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

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
                // KeepHours requires a resource (capacity is per-resource). A
                // placeholder has none, so the gesture is undefined.
                if (allocation.ResourceId is null)
                    return ServiceResult<AllocationReadDto>.Conflict(
                        "Cannot move with KeepHours on a placeholder allocation: no resource ⇒ no capacity reference. " +
                        "Use KeepPercent or assign the placeholder first.");

                var resourceId = allocation.ResourceId.Value;

                var oldCapacity = await CapacityInWindowAsync(
                    resourceId, allocation.PeriodStart, allocation.PeriodEnd, ct);
                if (oldCapacity.IsFailure)
                    return ServiceResult<AllocationReadDto>.Failure(oldCapacity.Error!);

                var newCapacity = await CapacityInWindowAsync(
                    resourceId, dto.NewPeriodStart, dto.NewPeriodEnd, ct);
                if (newCapacity.IsFailure)
                    return ServiceResult<AllocationReadDto>.Failure(newCapacity.Error!);

                if (newCapacity.Value <= TimeSpan.Zero)
                    return ServiceResult<AllocationReadDto>.Conflict(
                        "Cannot move with KeepHours: resource has zero capacity in the new window.");

                // Hours implied by current allocation at its current capacity.
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

        await repository.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    // ── Writes — placeholder transitions ────────────────────────────────────

    public async Task<ServiceResult<AllocationReadDto>> ConvertToPlaceholderAsync(
        Guid id, ConvertToPlaceholderDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        if (allocation.IsPlaceholder)
            return ServiceResult<AllocationReadDto>.Conflict("Allocation is already a placeholder.");

        // I4 (project root not closed/cancelled) — even a conversion should
        // refuse on a closed root.
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

        // Validate RoleSkillId and OwnerResourceId references exist.
        var refsCheck = await ValidatePlaceholderReferencesAsync<AllocationReadDto>(
            dto.RoleSkillId, dto.OwnerResourceId, ct);
        if (refsCheck is { } rc) return rc;

        try
        {
            allocation.ConvertToPlaceholder(dto.RoleSkillId, dto.OwnerResourceId);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> AssignToResourceAsync(
        Guid id, AssignToResourceDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        if (!allocation.IsPlaceholder)
            return ServiceResult<AllocationReadDto>.Conflict(
                "Only a placeholder allocation can be assigned to a resource.");

        // I3 — the resource must exist and be active (gated only on the
        // assigned branch per ADR-0016 §6).
        var resourceInfo = await db.Resources
            .AsNoTracking()
            .Where(r => r.Id == dto.ResourceId)
            .Select(r => new { r.IsActive })
            .FirstOrDefaultAsync(ct);

        if (resourceInfo is null)
            return ServiceResult<AllocationReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AssignToResourceDto.ResourceId)] = [$"Resource {dto.ResourceId} does not exist."]
            });

        if (!resourceInfo.IsActive)
            return ServiceResult<AllocationReadDto>.Conflict(
                $"Resource {dto.ResourceId} is inactive and cannot be allocated.");

        // I4 re-check.
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

        try
        {
            allocation.AssignTo(dto.ResourceId);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<ServiceResult<AllocationReadDto>> ChangeStatusAsync(
        Guid id, ChangeAllocationStatusDto dto, CancellationToken ct = default)
    {
        var allocation = await repository.GetByIdAsync(id, ct);
        if (allocation is null)
            return ServiceResult<AllocationReadDto>.NotFound($"Allocation {id} not found.");

        // I6: Hard requires the root project to be hard-committed. Applies to
        // both assigned and placeholder allocations (ADR-0015, ADR-0016 §6).
        if (dto.Status == AllocationStatus.Hard)
        {
            var hardCheck = await ValidateHardCommitmentAsync<AllocationReadDto>(
                allocation.ProjectNodeId, ct);
            if (hardCheck is { } hc) return hc;
        }

        try
        {
            allocation.ChangeStatus(dto.Status, dto.Reason);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

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
    //
    // Joins: Resource is conditional (placeholders have no resource). Skill
    // and OwnerResource are conditional (only set on placeholders). Implemented
    // via LEFT JOIN through `from ... in db.X.Where(...).DefaultIfEmpty()`.
    private IQueryable<AllocationReadDto> BuildReadQuery() =>
        from a in db.Allocations.AsNoTracking()
        join p in db.ProjectNodes.AsNoTracking() on a.ProjectNodeId equals p.Id
        from r in db.Resources.AsNoTracking()
            .Where(r => r.Id == a.ResourceId).DefaultIfEmpty()
        from s in db.Skills.AsNoTracking()
            .Where(s => s.Id == a.RoleSkillId).DefaultIfEmpty()
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
            RoleSkillId = a.RoleSkillId,
            RoleSkillName = s != null ? s.Name : null,
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
        // If capacity lookup fails for any reason, we still return the DTO
        // without ResolvedHours rather than failing the whole read — the rest
        // of the data is correct, and the sidecar endpoint exists for retry.
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
            RoleSkillId = dto.RoleSkillId,
            RoleSkillName = dto.RoleSkillName,
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

    // Runs the cross-aggregate gates for assigned creation: I1, I3, I4, I6.
    private async Task<ServiceResult<Unit>> ValidateAssignedCreatePreconditionsAsync(
        Guid resourceId, Guid projectNodeId, AllocationStatus status,
        CancellationToken ct)
    {
        var nodeInfo = await LoadNodeInfoForCreateAsync(projectNodeId, ct);
        if (nodeInfo.Result is { } nodeFail) return nodeFail;

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
        var statusCheck = await ValidateProjectStatusAsync<Unit>(nodeInfo.Path!, ct);
        if (statusCheck is { } sc) return sc;

        // I6
        if (status == AllocationStatus.Hard)
        {
            var hardCheck = await ValidateHardCommitmentAsync<Unit>(projectNodeId, ct);
            if (hardCheck is { } hc) return hc;
        }

        return ServiceResult.Ok();
    }

    // Runs the cross-aggregate gates for placeholder creation: I1, I4, I6,
    // plus existence of role/owner references. I3 is NOT applicable (no
    // resource — ADR-0016 §6).
    private async Task<ServiceResult<Unit>> ValidatePlaceholderCreatePreconditionsAsync(
        Guid projectNodeId, Guid roleSkillId, Guid? ownerResourceId,
        AllocationStatus status, CancellationToken ct)
    {
        var nodeInfo = await LoadNodeInfoForCreateAsync(projectNodeId, ct);
        if (nodeInfo.Result is { } nodeFail) return nodeFail;

        var refs = await ValidatePlaceholderReferencesAsync<Unit>(roleSkillId, ownerResourceId, ct);
        if (refs is { } r) return r;

        // I4
        var statusCheck = await ValidateProjectStatusAsync<Unit>(nodeInfo.Path!, ct);
        if (statusCheck is { } sc) return sc;

        // I6
        if (status == AllocationStatus.Hard)
        {
            var hardCheck = await ValidateHardCommitmentAsync<Unit>(projectNodeId, ct);
            if (hardCheck is { } hc) return hc;
        }

        return ServiceResult.Ok();
    }

    // Shared I1 check: project node exists and is at capacity-planning level.
    // Returns the node's Path on success (used to derive the root for I4/I6).
    private async Task<(ServiceResult<Unit>? Result, string? Path)> LoadNodeInfoForCreateAsync(
        Guid projectNodeId, CancellationToken ct)
    {
        var nodeInfo = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => new { p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (nodeInfo is null)
            return (ServiceResult.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"ProjectNode {projectNodeId} does not exist."]
            }), null);

        if (nodeInfo.NodeType != ProjectNodeType.Project && nodeInfo.NodeType != ProjectNodeType.Phase)
            return (ServiceResult.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] =
                    [$"Allocations are only allowed on Project or Phase nodes (got {nodeInfo.NodeType})."]
            }), null);

        return (null, nodeInfo.Path);
    }

    // Existence check for placeholder references. RoleSkillId is required;
    // OwnerResourceId is optional but, when supplied, must point to an
    // existing resource. Active-state is intentionally not checked for the
    // owner — they're a routing pointer, not the staffed resource (ADR-0016 §6).
    private async Task<ServiceResult<T>?> ValidatePlaceholderReferencesAsync<T>(
        Guid roleSkillId, Guid? ownerResourceId, CancellationToken ct)
    {
        var skillExists = await db.Skills.AnyAsync(s => s.Id == roleSkillId, ct);
        if (!skillExists)
            return ServiceResult<T>.Validation(new Dictionary<string, string[]>
            {
                ["RoleSkillId"] = [$"Skill {roleSkillId} does not exist."]
            });

        if (ownerResourceId is Guid o)
        {
            var ownerExists = await db.Resources.AnyAsync(r => r.Id == o, ct);
            if (!ownerExists)
                return ServiceResult<T>.Validation(new Dictionary<string, string[]>
                {
                    ["OwnerResourceId"] = [$"Resource {o} does not exist."]
                });
        }

        return null;
    }

    private async Task<ServiceResult<AllocationReadDto>> PersistNewAssignedAsync(
        Guid resourceId, Guid projectNodeId,
        DateOnly periodStart, DateOnly periodEnd,
        decimal percent, AllocationStatus status, string? notes,
        CancellationToken ct)
    {
        Allocation allocation;
        try
        {
            allocation = Allocation.Create(
                resourceId, projectNodeId, periodStart, periodEnd, percent, notes, status);
        }
        catch (DomainException ex)
        {
            return ServiceResult<AllocationReadDto>.Conflict(ex.Message);
        }

        await repository.AddAsync(allocation, ct);
        await repository.SaveChangesAsync(ct);

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

    // I6 enforcement (ADR-0015 §3): walks the materialized path to the root
    // Project and verifies its CommitmentLevel ∈ {Committed, Critical}.
    private async Task<ServiceResult<T>?> ValidateHardCommitmentAsync<T>(
        Guid projectNodeId, CancellationToken ct)
    {
        var nodePath = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == projectNodeId)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);

        if (nodePath is null)
            return ServiceResult<T>.Failure(ServiceError.Failure(
                $"ProjectNode {projectNodeId} disappeared while validating I6."));

        var rootIdString = nodePath.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdString, out var rootId))
            return ServiceResult<T>.Failure(ServiceError.Failure("ProjectNode has an invalid materialized path."));

        var rootLevel = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == rootId)
            .Select(p => (CommitmentLevel?)p.CommitmentLevel)
            .FirstOrDefaultAsync(ct);

        if (!IsHardCommittedLevel(rootLevel))
            return ServiceResult<T>.Conflict(
                $"Allocation cannot be set to Hard: the project root commitment level is " +
                $"'{rootLevel?.ToString() ?? "Unspecified"}'. Hard requires Committed or Critical.");

        return null;
    }

    private static ServiceResult<T> RangeValidation<T>() =>
        ServiceResult<T>.Validation(new Dictionary<string, string[]>
        {
            ["range"] = ["'from' must be on or before 'to'."]
        });
}
