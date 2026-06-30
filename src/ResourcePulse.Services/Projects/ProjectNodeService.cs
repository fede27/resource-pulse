using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Services.Projects;

public sealed class ProjectNodeService(
    IRepository<ProjectNode, Guid> repository,
    ResourcePulseDbContext db,
    IMapper mapper,
    ICommitmentPolicyService commitmentPolicy) : IProjectNodeService
{
    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<ProjectNodeReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<ProjectNodeReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult<ProjectNodeReadDto>.NotFound($"ProjectNode {id} not found.");
        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        return ServiceResult<ProjectNodeReadDto>.Success(ToDtoWithMetrics(node, policy));
    }

    public async Task<ServiceResult<IReadOnlyList<ProjectNodeReadDto>>> GetSubtreeAsync(Guid id, CancellationToken ct = default)
    {
        var rootPath = await db.ProjectNodes
            .Where(p => p.Id == id)
            .Select(p => p.Path)
            .FirstOrDefaultAsync(ct);

        if (rootPath is null)
            return ServiceResult<IReadOnlyList<ProjectNodeReadDto>>.NotFound($"ProjectNode {id} not found.");

        var prefix = rootPath + "/";
        var nodes = await db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.Id == id || p.Path.StartsWith(prefix))
            .OrderBy(p => p.Path)
            .ToListAsync(ct);

        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        var dtos = nodes.Select(n => ToDtoWithMetrics(n, policy)).ToList();
        return ServiceResult<IReadOnlyList<ProjectNodeReadDto>>.Success(dtos);
    }

    // ── CRUD ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ProjectNodeReadDto>> CreateAsync(CreateProjectNodeDto dto, CancellationToken ct = default)
    {
        ProjectNode node;
        try
        {
            if (dto.NodeType == ProjectNodeType.Project)
            {
                if (dto.LeadResourceId is { } lid && lid != Guid.Empty)
                {
                    var exists = await db.Resources.AnyAsync(r => r.Id == lid, ct);
                    if (!exists)
                        return ServiceResult<ProjectNodeReadDto>.Validation(new Dictionary<string, string[]>
                        {
                            [nameof(CreateProjectNodeDto.LeadResourceId)] = [$"Resource {lid} does not exist."]
                        });
                }

                node = ProjectNode.CreateRoot(
                    dto.Name, dto.Code,
                    dto.Type!.Value, dto.CommitmentLevel!.Value, dto.LeadResourceId, dto.Client);
            }
            else
            {
                var parent = await repository.GetByIdAsync(dto.ParentId!.Value, ct);
                if (parent is null)
                    return ServiceResult<ProjectNodeReadDto>.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(CreateProjectNodeDto.ParentId)] = [$"Parent {dto.ParentId} does not exist."]
                    });

                node = ProjectNode.CreateChild(parent, dto.NodeType, dto.Name, dto.Code);
            }
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectNodeReadDto>.Conflict(ex.Message);
        }

        await repository.AddAsync(node, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<ProjectNodeReadDto>.Conflict("Code is already in use under this parent.");
        }

        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        return ServiceResult<ProjectNodeReadDto>.Success(ToDtoWithMetrics(node, policy));
    }

    public async Task<ServiceResult<ProjectNodeReadDto>> UpdateAsync(Guid id, UpdateProjectNodeDto dto, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult<ProjectNodeReadDto>.NotFound($"ProjectNode {id} not found.");

        try
        {
            node.Rename(dto.Name);
            node.ChangeCode(dto.Code);
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectNodeReadDto>.Conflict(ex.Message);
        }

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<ProjectNodeReadDto>.Conflict("Code is already in use under this parent.");
        }

        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        return ServiceResult<ProjectNodeReadDto>.Success(ToDtoWithMetrics(node, policy));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        var hasChildren = await db.ProjectNodes.AnyAsync(p => p.ParentId == id, ct);
        if (hasChildren) return ServiceResult.Conflict("Cannot delete a node with children. Reparent or delete its children first.");

        repository.Remove(node);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Reparent ────────────────────────────────────────────────────────────

    public async Task<ServiceResult<Unit>> ReparentAsync(Guid id, ReparentDto dto, CancellationToken ct = default)
    {
        var node = await db.ProjectNodes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        var newParent = await db.ProjectNodes.FirstOrDefaultAsync(p => p.Id == dto.NewParentId, ct);
        if (newParent is null)
            return ServiceResult.Validation(new Dictionary<string, string[]>
            {
                [nameof(ReparentDto.NewParentId)] = [$"Parent {dto.NewParentId} does not exist."]
            });

        // Load all descendants (path prefix match). Tracked, so updates flow through SaveChangesAsync.
        var oldPrefix = node.Path + "/";
        var descendants = await db.ProjectNodes
            .Where(p => p.Path.StartsWith(oldPrefix))
            .ToListAsync(ct);

        try
        {
            node.Reparent(newParent, descendants);
        }
        catch (DomainException ex)
        {
            return ServiceResult.Conflict(ex.Message);
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Dates (Project + Phase) ─────────────────────────────────────────────

    public Task<ServiceResult<Unit>> BaselineAsync(Guid id, BaselineDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.Baseline(dto.Start, dto.End), ct);

    public Task<ServiceResult<Unit>> RebaselineAsync(Guid id, RebaselineDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.Rebaseline(dto.Start, dto.End, dto.Reason), ct);

    public Task<ServiceResult<Unit>> ReplanAsync(Guid id, ReplanDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.Replan(dto.Start, dto.End), ct);

    public Task<ServiceResult<Unit>> BackfillActualsAsync(Guid id, BackfillActualsDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.BackfillActuals(dto.Start, dto.End), ct);

    public Task<ServiceResult<Unit>> SetPlanningModeAsync(Guid id, SetPlanningModeDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.SetPlanningMode(dto.Mode, dto.EstimatedWork), ct);

    public Task<ServiceResult<Unit>> UpdateEstimatedWorkAsync(Guid id, UpdateEstimatedWorkDto dto, CancellationToken ct = default) =>
        MutateAsync(id, n => n.UpdateEstimatedWork(dto.EstimatedWork), ct);

    public async Task<ServiceResult<Unit>> RecalculatePlannedFromChildrenAsync(Guid id, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        var children = await db.ProjectNodes.Where(p => p.ParentId == id).ToListAsync(ct);
        try
        {
            node.RecalculatePlannedFromChildren(children);
        }
        catch (DomainException ex)
        {
            return ServiceResult.Conflict(ex.Message);
        }
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Unit>> RecalculateBaselineFromChildrenAsync(Guid id, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        var children = await db.ProjectNodes.Where(p => p.ParentId == id).ToListAsync(ct);
        try
        {
            node.RecalculateBaselineFromChildren(children);
        }
        catch (DomainException ex)
        {
            return ServiceResult.Conflict(ex.Message);
        }
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Tags ────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ProjectNodeTagDto>> AddTagAsync(Guid id, AddProjectNodeTagDto dto, CancellationToken ct = default)
    {
        var node = await LoadWithOwnedAsync(id, ct);
        if (node is null) return ServiceResult<ProjectNodeTagDto>.NotFound($"ProjectNode {id} not found.");

        var tagExists = await db.Tags.AnyAsync(t => t.Id == dto.TagId, ct);
        if (!tagExists)
            return ServiceResult<ProjectNodeTagDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddProjectNodeTagDto.TagId)] = [$"Tag {dto.TagId} does not exist."]
            });

        try
        {
            node.AddTag(dto.TagId);
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectNodeTagDto>.Conflict(ex.Message);
        }

        var added = node.Tags.Single(t => t.TagId == dto.TagId);
        db.MarkOwnedAdded(node, n => n.Tags, added);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ProjectNodeTagDto>.Success(new ProjectNodeTagDto { TagId = dto.TagId });
    }

    public async Task<ServiceResult<Unit>> RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct = default)
    {
        var node = await LoadWithOwnedAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        try
        {
            node.RemoveTag(tagId);
        }
        catch (DomainException ex)
        {
            return ServiceResult.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Project-only: metadata ──────────────────────────────────────────────

    public async Task<ServiceResult<ProjectNodeReadDto>> UpdateProjectAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult<ProjectNodeReadDto>.NotFound($"ProjectNode {id} not found.");
        if (node.NodeType != ProjectNodeType.Project)
            return ServiceResult<ProjectNodeReadDto>.NotFound($"ProjectNode {id} is not a Project root.");

        if (dto.LeadResourceId is { } lid && lid != Guid.Empty)
        {
            var exists = await db.Resources.AnyAsync(r => r.Id == lid, ct);
            if (!exists)
                return ServiceResult<ProjectNodeReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(UpdateProjectDto.LeadResourceId)] = [$"Resource {lid} does not exist."]
                });
        }

        // Cascade demotion guard (ADR-0015 §4). Quando il downgrade attraversa
        // la soglia hard-committed → non-hard, le allocazioni Hard sulla
        // subtree del progetto vanno demote esplicitamente: o il chiamante
        // conferma con ConfirmDemoteHardAllocations = true e procediamo, o
        // restituiamo Conflict con il conteggio.
        // Hard-commit threshold read from CommitmentPolicy (ADR-0020) — no longer
        // cabled; same single source as the PlanCommandService Hard gate (I6).
        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        var oldLevel = node.CommitmentLevel;
        var newLevel = dto.CommitmentLevel;
        var crossesHardThreshold = policy.IsHardCommitted(oldLevel) && !policy.IsHardCommitted(newLevel);

        List<Allocation> hardAllocationsToDemote = [];
        if (crossesHardThreshold)
        {
            // Subtree = root + children whose path starts with "/{rootId}/".
            var subtreePathPrefix = node.Path + "/";
            hardAllocationsToDemote = await (
                from a in db.Allocations
                join p in db.ProjectNodes on a.ProjectNodeId equals p.Id
                where a.Status == AllocationStatus.Hard
                   && (p.Id == node.Id || p.Path.StartsWith(subtreePathPrefix))
                select a
            ).ToListAsync(ct);

            if (hardAllocationsToDemote.Count > 0 && !dto.ConfirmDemoteHardAllocations)
            {
                return ServiceResult<ProjectNodeReadDto>.Conflict(
                    $"Downgrading commitment from '{oldLevel}' to '{newLevel}' would demote " +
                    $"{hardAllocationsToDemote.Count} Hard allocation(s) on this project's subtree to Tentative. " +
                    "Re-submit with ConfirmDemoteHardAllocations = true to proceed.");
            }
        }

        try
        {
            node.ChangeType(dto.Type);
            node.ChangeCommitmentLevel(dto.CommitmentLevel);
            node.AssignLead(dto.LeadResourceId);
            node.ChangeClient(dto.Client);

            foreach (var a in hardAllocationsToDemote)
                a.ChangeStatus(AllocationStatus.Tentative, "ProjectCommitmentDowngrade");
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectNodeReadDto>.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        // Reuse the policy already fetched above for the cascade-demotion check.
        return ServiceResult<ProjectNodeReadDto>.Success(ToDtoWithMetrics(node, policy));
    }

    // ── Project-only: state transitions ─────────────────────────────────────

    public Task<ServiceResult<Unit>> StartAsync(Guid id, CancellationToken ct = default) =>
        ProjectOnlyMutateAsync(id, n => n.Start(), ct);

    public Task<ServiceResult<Unit>> CompleteAsync(Guid id, CancellationToken ct = default) =>
        ProjectOnlyMutateAsync(id, n => n.Complete(), ct);

    public Task<ServiceResult<Unit>> SuspendAsync(Guid id, ReasonDto dto, CancellationToken ct = default) =>
        ProjectOnlyMutateAsync(id, n => n.Suspend(dto.Reason), ct);

    public Task<ServiceResult<Unit>> ResumeAsync(Guid id, CancellationToken ct = default) =>
        ProjectOnlyMutateAsync(id, n => n.Resume(), ct);

    public Task<ServiceResult<Unit>> CancelAsync(Guid id, ReasonDto dto, CancellationToken ct = default) =>
        ProjectOnlyMutateAsync(id, n => n.Cancel(dto.Reason), ct);

    // ── Project-only: skill requirements ────────────────────────────────────

    public async Task<ServiceResult<ProjectSkillRequirementDto>> AddSkillRequirementAsync(
        Guid id, AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct = default)
    {
        var node = await LoadWithOwnedAsync(id, ct);
        if (node is null) return ServiceResult<ProjectSkillRequirementDto>.NotFound($"ProjectNode {id} not found.");
        if (node.NodeType != ProjectNodeType.Project)
            return ServiceResult<ProjectSkillRequirementDto>.NotFound($"ProjectNode {id} is not a Project root.");

        var skillExists = await db.Skills.AnyAsync(s => s.Id == dto.SkillId, ct);
        if (!skillExists)
            return ServiceResult<ProjectSkillRequirementDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddOrUpdateProjectSkillRequirementDto.SkillId)] = [$"Skill {dto.SkillId} does not exist."]
            });

        try
        {
            node.AddSkillRequirement(dto.SkillId, dto.MinLevel);
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectSkillRequirementDto>.Conflict(ex.Message);
        }

        var added = node.SkillRequirements.Single(r => r.SkillId == dto.SkillId);
        db.MarkOwnedAdded(node, n => n.SkillRequirements, added);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ProjectSkillRequirementDto>.Success(
            new ProjectSkillRequirementDto { SkillId = dto.SkillId, MinLevel = dto.MinLevel });
    }

    public async Task<ServiceResult<ProjectSkillRequirementDto>> UpdateSkillRequirementLevelAsync(
        Guid id, Guid skillId, AddOrUpdateProjectSkillRequirementDto dto, CancellationToken ct = default)
    {
        if (skillId != dto.SkillId)
            return ServiceResult<ProjectSkillRequirementDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddOrUpdateProjectSkillRequirementDto.SkillId)] = ["SkillId in route and body must match."]
            });

        var node = await LoadWithOwnedAsync(id, ct);
        if (node is null) return ServiceResult<ProjectSkillRequirementDto>.NotFound($"ProjectNode {id} not found.");
        if (node.NodeType != ProjectNodeType.Project)
            return ServiceResult<ProjectSkillRequirementDto>.NotFound($"ProjectNode {id} is not a Project root.");

        try
        {
            node.UpdateSkillRequirementLevel(skillId, dto.MinLevel);
        }
        catch (DomainException ex)
        {
            return ServiceResult<ProjectSkillRequirementDto>.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ProjectSkillRequirementDto>.Success(
            new ProjectSkillRequirementDto { SkillId = skillId, MinLevel = dto.MinLevel });
    }

    public async Task<ServiceResult<Unit>> RemoveSkillRequirementAsync(Guid id, Guid skillId, CancellationToken ct = default)
    {
        var node = await LoadWithOwnedAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");
        if (node.NodeType != ProjectNodeType.Project)
            return ServiceResult.NotFound($"ProjectNode {id} is not a Project root.");

        try
        {
            node.RemoveSkillRequirement(skillId);
        }
        catch (DomainException ex)
        {
            return ServiceResult.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Listing: active projects in range ───────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<ProjectNodeReadDto>>> GetProjectsActiveInRangeAsync(
        DateOnly from, DateOnly to, DateSource dateSource, CancellationToken ct = default)
    {
        if (from > to)
            return ServiceResult<IReadOnlyList<ProjectNodeReadDto>>.Validation(new Dictionary<string, string[]>
            {
                ["from"] = ["'from' must be on or before 'to'."]
            });

        IQueryable<ProjectNode> baseQuery = db.ProjectNodes
            .AsNoTracking()
            .Where(p => p.NodeType == ProjectNodeType.Project);

        // For each date source: the project is "active in [from, to]" if its date range
        // overlaps the requested window. Null endpoints are treated as open-ended.
        IQueryable<ProjectNode> filtered = dateSource switch
        {
            DateSource.Planned => baseQuery.Where(p =>
                (p.PlannedStart == null || p.PlannedStart <= to) &&
                (p.PlannedEnd == null || p.PlannedEnd >= from)),
            DateSource.Baseline => baseQuery.Where(p =>
                (p.BaselineStart == null || p.BaselineStart <= to) &&
                (p.BaselineEnd == null || p.BaselineEnd >= from)),
            DateSource.Effective => baseQuery.Where(p =>
                ((p.ActualStart ?? p.PlannedStart) == null || (p.ActualStart ?? p.PlannedStart) <= to) &&
                ((p.ActualEnd ?? p.PlannedEnd) == null || (p.ActualEnd ?? p.PlannedEnd) >= from)),
            _ => baseQuery
        };

        var results = await filtered
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        var dtos = results.Select(n => ToDtoWithMetrics(n, policy)).ToList();
        return ServiceResult<IReadOnlyList<ProjectNodeReadDto>>.Success(dtos);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ServiceResult<Unit>> MutateAsync(Guid id, Action<ProjectNode> mutator, CancellationToken ct)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");

        try
        {
            mutator(node);
        }
        catch (DomainException ex)
        {
            return ServiceResult.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult<Unit>> ProjectOnlyMutateAsync(Guid id, Action<ProjectNode> mutator, CancellationToken ct)
    {
        var node = await repository.GetByIdAsync(id, ct);
        if (node is null) return ServiceResult.NotFound($"ProjectNode {id} not found.");
        if (node.NodeType != ProjectNodeType.Project)
            return ServiceResult.NotFound($"ProjectNode {id} is not a Project root.");

        try
        {
            mutator(node);
        }
        catch (DomainException ex)
        {
            return ServiceResult.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private ProjectNodeReadDto ToDtoWithMetrics(ProjectNode node, CommitmentPolicyConfiguration policy)
    {
        var dto = mapper.Map<ProjectNodeReadDto>(node);
        dto.ScheduleVarianceStart = ProjectNodeMetrics.ScheduleVarianceStart(node);
        dto.ScheduleVarianceEnd = ProjectNodeMetrics.ScheduleVarianceEnd(node);
        dto.ForecastVarianceEnd = ProjectNodeMetrics.ForecastVarianceEnd(node);
        dto.IsLate = ProjectNodeMetrics.IsLate(node);
        dto.DerivedStatus = ProjectNodeMetrics.DerivedStatus(node);

        // M3: project-level provenance. "Proposed" = the complement of the
        // hard-commit threshold (ADR-0020 / CommitmentPolicy). Project roots only;
        // null on Phase/WorkPackage (no commitment level).
        dto.IsProposed = node.NodeType == ProjectNodeType.Project
            ? !policy.IsHardCommitted(node.CommitmentLevel)
            : null;
        return dto;
    }

    // FindAsync (used by the generic repository) does not include OwnsMany
    // navigations. Operations that mutate the owned graph (tags, skill
    // requirements) need the collections populated so the domain can enforce
    // invariants and EF can correctly diff change tracking. Use this loader
    // anywhere we touch owned state.
    private Task<ProjectNode?> LoadWithOwnedAsync(Guid id, CancellationToken ct) =>
        db.ProjectNodes.FirstOrDefaultAsync(n => n.Id == id, ct);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
