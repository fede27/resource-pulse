using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Resources;

public sealed class ResourceService(
    IRepository<Resource, Guid> repository,
    ResourcePulseDbContext db,
    IMapper mapper,
    ICurrentUserAccessor currentUserAccessor) : IResourceService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<ResourceReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<ResourceReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(r => r.Id == id)
            .ProjectToType<ResourceReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<ResourceReadDto>.NotFound($"Resource {id} not found.")
            : ServiceResult<ResourceReadDto>.Success(dto);
    }

    public async Task<ServiceResult<ResourceReadDto>> CreateAsync(CreateResourceDto dto, CancellationToken ct = default)
    {
        Guid calendarId;
        if (dto.BusinessCalendarId is null || dto.BusinessCalendarId == Guid.Empty)
        {
            // Resolve the default calendar when none is specified.
            var defaultId = await db.BusinessCalendars
                .Where(c => c.IsDefault)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);
            if (defaultId == Guid.Empty)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.BusinessCalendarId)] =
                        ["BusinessCalendarId is required and no default BusinessCalendar exists."]
                });
            }
            calendarId = defaultId;
        }
        else
        {
            calendarId = dto.BusinessCalendarId.Value;
            var exists = await db.BusinessCalendars.AnyAsync(c => c.Id == calendarId, ct);
            if (!exists)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.BusinessCalendarId)] =
                        [$"BusinessCalendar {calendarId} does not exist."]
                });
            }
        }

        if (dto.TeamId is { } teamId && teamId != Guid.Empty)
        {
            var teamExists = await db.Teams.AnyAsync(t => t.Id == teamId, ct);
            if (!teamExists)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.TeamId)] = [$"Team {teamId} does not exist."]
                });
            }
        }

        if (dto.RoleId is { } roleId && roleId != Guid.Empty)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId, ct);
            if (!roleExists)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.RoleId)] = [$"Role {roleId} does not exist."]
                });
            }
        }

        if (dto.Skills is { Count: > 0 } skills)
        {
            var skillIds = skills.Select(s => s.SkillId).Distinct().ToArray();
            var foundCount = await db.Skills.CountAsync(s => skillIds.Contains(s.Id), ct);
            if (foundCount != skillIds.Length)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.Skills)] = ["One or more referenced skills do not exist."]
                });
            }
        }

        if (dto.Tags is { Count: > 0 } tags)
        {
            var tagIds = tags.Select(t => t.TagId).Distinct().ToArray();
            var foundCount = await db.Tags.CountAsync(t => tagIds.Contains(t.Id), ct);
            if (foundCount != tagIds.Length)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(CreateResourceDto.Tags)] = ["One or more referenced tags do not exist."]
                });
            }
        }

        Resource resource;
        try
        {
            resource = Resource.Create(dto.Name, calendarId);
            resource.SetEmail(dto.Email);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(CreateResourceDto.Email)] = [ex.Message]
            });
        }
        if (dto.TeamId is { } tid && tid != Guid.Empty)
            resource.AssignToTeam(tid);
        if (dto.RoleId is { } rid && rid != Guid.Empty)
            resource.AssignToRole(rid);
        if (!string.IsNullOrWhiteSpace(dto.UserSub))
            resource.LinkToUser(dto.UserSub);

        if (dto.Windows is not null)
        {
            foreach (var w in dto.Windows)
                resource.AddWorkWindowOverride(WorkWindow.Create(w.DayOfWeek, w.StartTime, w.EndTime, w.ValidFrom, w.ValidTo));
        }
        if (dto.Adjustments is not null)
        {
            foreach (var a in dto.Adjustments)
                resource.AddAdjustment(IndividualAdjustment.Create(a.DateFrom, a.DateTo, a.Type, a.Hours, a.Reason, a.Notes));
        }
        if (dto.Skills is not null)
        {
            foreach (var s in dto.Skills)
                resource.AddSkill(s.SkillId, s.Level);
        }
        if (dto.Tags is not null)
        {
            foreach (var t in dto.Tags)
                resource.AddTag(t.TagId);
        }

        await repository.AddAsync(resource, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<ResourceReadDto>.Conflict(DescribeUniqueViolation(ex));
        }
        return ServiceResult<ResourceReadDto>.Success(mapper.Map<ResourceReadDto>(resource));
    }

    public async Task<ServiceResult<ResourceReadDto>> UpdateAsync(Guid id, UpdateResourceDto dto, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(id, ct);
        if (resource is null) return ServiceResult<ResourceReadDto>.NotFound($"Resource {id} not found.");

        if (resource.BusinessCalendarId != dto.BusinessCalendarId)
        {
            var exists = await db.BusinessCalendars.AnyAsync(c => c.Id == dto.BusinessCalendarId, ct);
            if (!exists)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(UpdateResourceDto.BusinessCalendarId)] =
                        [$"BusinessCalendar {dto.BusinessCalendarId} does not exist."]
                });
            }
            resource.ChangeBusinessCalendar(dto.BusinessCalendarId);
        }

        if (dto.RoleId is { } roleId && roleId != Guid.Empty && resource.RoleId != roleId)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId, ct);
            if (!roleExists)
            {
                return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(UpdateResourceDto.RoleId)] = [$"Role {roleId} does not exist."]
                });
            }
        }

        resource.Rename(dto.Name);
        try
        {
            resource.SetEmail(dto.Email);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult<ResourceReadDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(UpdateResourceDto.Email)] = [ex.Message]
            });
        }
        resource.AssignToRole(dto.RoleId);
        if (dto.IsActive) resource.Activate(); else resource.Deactivate();
        resource.LinkToUser(dto.UserSub);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<ResourceReadDto>.Conflict(DescribeUniqueViolation(ex));
        }
        return ServiceResult<ResourceReadDto>.Success(mapper.Map<ResourceReadDto>(resource));
    }

    public async Task<ServiceResult<Unit>> AssignRoleAsync(Guid resourceId, AssignRoleDto dto, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        if (dto.RoleId is { } roleId && roleId != Guid.Empty)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId, ct);
            if (!roleExists)
                return ServiceResult.Validation(new Dictionary<string, string[]>
                {
                    [nameof(AssignRoleDto.RoleId)] = [$"Role {roleId} does not exist."]
                });
        }

        resource.AssignToRole(dto.RoleId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(id, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {id} not found.");

        repository.Remove(resource);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<WorkWindowDto>> AddWorkWindowAsync(Guid resourceId, WorkWindowDto dto, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult<WorkWindowDto>.NotFound($"Resource {resourceId} not found.");

        var window = WorkWindow.Create(dto.DayOfWeek, dto.StartTime, dto.EndTime, dto.ValidFrom, dto.ValidTo);
        resource.AddWorkWindowOverride(window);
        db.MarkOwnedAdded(resource, r => r.WorkWindows, window);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<WorkWindowDto>.Success(mapper.Map<WorkWindowDto>(window));
    }

    public async Task<ServiceResult<Unit>> RemoveWorkWindowAsync(Guid resourceId, Guid windowId, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        resource.RemoveWorkWindowOverride(windowId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IndividualAdjustmentDto>> AddAdjustmentAsync(Guid resourceId, IndividualAdjustmentDto dto, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult<IndividualAdjustmentDto>.NotFound($"Resource {resourceId} not found.");

        var adjustment = IndividualAdjustment.Create(dto.DateFrom, dto.DateTo, dto.Type, dto.Hours, dto.Reason, dto.Notes);
        resource.AddAdjustment(adjustment);
        db.MarkOwnedAdded(resource, r => r.Adjustments, adjustment);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<IndividualAdjustmentDto>.Success(mapper.Map<IndividualAdjustmentDto>(adjustment));
    }

    public async Task<ServiceResult<Unit>> RemoveAdjustmentAsync(Guid resourceId, Guid adjustmentId, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        resource.RemoveAdjustment(adjustmentId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Unit>> AssignTeamAsync(Guid resourceId, AssignTeamDto dto, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        if (dto.TeamId is { } teamId && teamId != Guid.Empty)
        {
            var teamExists = await db.Teams.AnyAsync(t => t.Id == teamId, ct);
            if (!teamExists)
                return ServiceResult.Validation(new Dictionary<string, string[]>
                {
                    [nameof(AssignTeamDto.TeamId)] = [$"Team {teamId} does not exist."]
                });
        }

        resource.AssignToTeam(dto.TeamId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<ResourceSkillDto>> AddSkillAsync(Guid resourceId, AddOrUpdateResourceSkillDto dto, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult<ResourceSkillDto>.NotFound($"Resource {resourceId} not found.");

        var skillExists = await db.Skills.AnyAsync(s => s.Id == dto.SkillId, ct);
        if (!skillExists)
            return ServiceResult<ResourceSkillDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddOrUpdateResourceSkillDto.SkillId)] = [$"Skill {dto.SkillId} does not exist."]
            });

        try
        {
            resource.AddSkill(dto.SkillId, dto.Level);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult<ResourceSkillDto>.Conflict(ex.Message);
        }

        var added = resource.Skills.Single(s => s.SkillId == dto.SkillId);
        db.MarkOwnedAdded(resource, r => r.Skills, added);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ResourceSkillDto>.Success(mapper.Map<ResourceSkillDto>(added));
    }

    public async Task<ServiceResult<ResourceSkillDto>> UpdateSkillLevelAsync(
        Guid resourceId, Guid skillId, AddOrUpdateResourceSkillDto dto, CancellationToken ct = default)
    {
        if (skillId != dto.SkillId)
            return ServiceResult<ResourceSkillDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddOrUpdateResourceSkillDto.SkillId)] = ["SkillId in the route and body must match."]
            });

        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult<ResourceSkillDto>.NotFound($"Resource {resourceId} not found.");

        try
        {
            resource.UpdateSkillLevel(skillId, dto.Level);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult<ResourceSkillDto>.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

        var updated = resource.Skills.Single(s => s.SkillId == skillId);
        return ServiceResult<ResourceSkillDto>.Success(mapper.Map<ResourceSkillDto>(updated));
    }

    public async Task<ServiceResult<Unit>> RemoveSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        try
        {
            resource.RemoveSkill(skillId);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<ResourceTagDto>> AddTagAsync(Guid resourceId, AddResourceTagDto dto, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult<ResourceTagDto>.NotFound($"Resource {resourceId} not found.");

        var tagExists = await db.Tags.AnyAsync(t => t.Id == dto.TagId, ct);
        if (!tagExists)
            return ServiceResult<ResourceTagDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(AddResourceTagDto.TagId)] = [$"Tag {dto.TagId} does not exist."]
            });

        try
        {
            resource.AddTag(dto.TagId);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult<ResourceTagDto>.Conflict(ex.Message);
        }

        var added = resource.Tags.Single(t => t.TagId == dto.TagId);
        db.MarkOwnedAdded(resource, r => r.Tags, added);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ResourceTagDto>.Success(new ResourceTagDto { TagId = dto.TagId });
    }

    public async Task<ServiceResult<Unit>> RemoveTagAsync(Guid resourceId, Guid tagId, CancellationToken ct = default)
    {
        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        try
        {
            resource.RemoveTag(tagId);
        }
        catch (Common.Domain.DomainException ex)
        {
            return ServiceResult.NotFound(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public Task<ServiceResult<ResourceSkillDto>> ApproveSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default) =>
        TransitionSkillAsync(resourceId, skillId, (resource, reviewerId, now) =>
            resource.ApproveSkill(skillId, reviewerId, now), ct);

    public Task<ServiceResult<ResourceSkillDto>> RejectSkillAsync(Guid resourceId, Guid skillId, CancellationToken ct = default) =>
        TransitionSkillAsync(resourceId, skillId, (resource, reviewerId, now) =>
            resource.RejectSkill(skillId, reviewerId, now), ct);

    public Task<ServiceResult<ResourceSkillDto>> ReturnSkillToPendingAsync(Guid resourceId, Guid skillId, CancellationToken ct = default) =>
        TransitionSkillAsync(resourceId, skillId, (resource, _, _) =>
            resource.ReturnSkillToPending(skillId), ct);

    private async Task<ServiceResult<ResourceSkillDto>> TransitionSkillAsync(
        Guid resourceId,
        Guid skillId,
        Action<Resource, Guid, DateTime> transition,
        CancellationToken ct)
    {
        // Reviewer identity comes from the auth subject, then mapped to a
        // Resource via UserSub. The mapping is required so approval provenance
        // is tracked as a Resource FK (the supervisor's resource record), not
        // an opaque sub string.
        var reviewerResult = await ResolveReviewerResourceIdAsync(ct);
        if (!reviewerResult.IsSuccess)
            return ServiceResult<ResourceSkillDto>.Forbidden(reviewerResult.Error!.Message);

        var resource = await LoadWithOwnedAsync(resourceId, ct);
        if (resource is null)
            return ServiceResult<ResourceSkillDto>.NotFound($"Resource {resourceId} not found.");

        if (!resource.Skills.Any(s => s.SkillId == skillId))
            return ServiceResult<ResourceSkillDto>.NotFound(
                $"Resource {resourceId} does not have skill {skillId}.");

        try
        {
            transition(resource, reviewerResult.Value, DateTime.UtcNow);
        }
        catch (Common.Domain.DomainException ex)
        {
            // The skill exists on this resource (checked above), so any
            // DomainException raised by the transition is an illegal state
            // change — surface it as Conflict.
            return ServiceResult<ResourceSkillDto>.Conflict(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

        var updated = resource.Skills.Single(s => s.SkillId == skillId);
        return ServiceResult<ResourceSkillDto>.Success(mapper.Map<ResourceSkillDto>(updated));
    }

    private async Task<ServiceResult<Guid>> ResolveReviewerResourceIdAsync(CancellationToken ct)
    {
        if (!currentUserAccessor.IsAuthenticated)
            return ServiceResult<Guid>.Forbidden("Authentication is required to review skills.");

        var sub = currentUserAccessor.User.Sub;
        if (string.IsNullOrEmpty(sub))
            return ServiceResult<Guid>.Forbidden("Current user has no subject identifier.");

        var reviewerId = await db.Resources
            .Where(r => r.UserSub == sub)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        return reviewerId is null
            ? ServiceResult<Guid>.Forbidden("Current user is not linked to a resource and cannot review skills.")
            : ServiceResult<Guid>.Success(reviewerId.Value);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    // Map the index name in the Postgres error message to a user-friendly
    // explanation. Falls back to a generic message when the constraint is
    // unknown — better that than a leaked SQL string.
    private static string DescribeUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? string.Empty;
        if (message.Contains("ux_resources_email", StringComparison.OrdinalIgnoreCase))
            return "Another resource is already using this email address.";
        if (message.Contains("ux_resources_user_sub", StringComparison.OrdinalIgnoreCase))
            return "Another resource is already linked to this user.";
        return "A unique-constraint violation prevented saving this resource.";
    }

    // FindAsync (used by the generic repository) does not include OwnsMany
    // navigations. Operations that mutate the owned graph (work windows,
    // adjustments, skills, tags) need the collections populated so the domain
    // can enforce invariants and EF can correctly diff change tracking. Use
    // this loader anywhere we touch owned state.
    private Task<Resource?> LoadWithOwnedAsync(Guid id, CancellationToken ct) =>
        db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct);
}
