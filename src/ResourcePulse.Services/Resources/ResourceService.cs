using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
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
    IMapper mapper) : IResourceService
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

        var resource = Resource.Create(dto.Name, calendarId);
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

        await repository.AddAsync(resource, ct);
        await repository.SaveChangesAsync(ct);
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

        resource.Rename(dto.Name);
        if (dto.IsActive) resource.Activate(); else resource.Deactivate();

        await repository.SaveChangesAsync(ct);
        return ServiceResult<ResourceReadDto>.Success(mapper.Map<ResourceReadDto>(resource));
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
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult<WorkWindowDto>.NotFound($"Resource {resourceId} not found.");

        var window = WorkWindow.Create(dto.DayOfWeek, dto.StartTime, dto.EndTime, dto.ValidFrom, dto.ValidTo);
        resource.AddWorkWindowOverride(window);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<WorkWindowDto>.Success(mapper.Map<WorkWindowDto>(window));
    }

    public async Task<ServiceResult<Unit>> RemoveWorkWindowAsync(Guid resourceId, Guid windowId, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        resource.RemoveWorkWindowOverride(windowId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IndividualAdjustmentDto>> AddAdjustmentAsync(Guid resourceId, IndividualAdjustmentDto dto, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult<IndividualAdjustmentDto>.NotFound($"Resource {resourceId} not found.");

        var adjustment = IndividualAdjustment.Create(dto.DateFrom, dto.DateTo, dto.Type, dto.Hours, dto.Reason, dto.Notes);
        resource.AddAdjustment(adjustment);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<IndividualAdjustmentDto>.Success(mapper.Map<IndividualAdjustmentDto>(adjustment));
    }

    public async Task<ServiceResult<Unit>> RemoveAdjustmentAsync(Guid resourceId, Guid adjustmentId, CancellationToken ct = default)
    {
        var resource = await repository.GetByIdAsync(resourceId, ct);
        if (resource is null) return ServiceResult.NotFound($"Resource {resourceId} not found.");

        resource.RemoveAdjustment(adjustmentId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
