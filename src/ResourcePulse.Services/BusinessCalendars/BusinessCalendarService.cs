using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.BusinessCalendars;

public sealed class BusinessCalendarService(
    IRepository<BusinessCalendar, Guid> repository,
    ResourcePulseDbContext db,
    IMapper mapper) : IBusinessCalendarService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<BusinessCalendarReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<BusinessCalendarReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(c => c.Id == id)
            .ProjectToType<BusinessCalendarReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<BusinessCalendarReadDto>.NotFound($"BusinessCalendar {id} not found.")
            : ServiceResult<BusinessCalendarReadDto>.Success(dto);
    }

    public async Task<ServiceResult<BusinessCalendarReadDto>> CreateAsync(
        CreateBusinessCalendarDto dto,
        CancellationToken ct = default)
    {
        var calendar = BusinessCalendar.Create(dto.Name, dto.IsDefault);
        if (dto.Windows is not null)
        {
            foreach (var w in dto.Windows)
                calendar.AddWorkWindow(WorkWindow.Create(w.DayOfWeek, w.StartTime, w.EndTime, w.ValidFrom, w.ValidTo));
        }

        // If creating as default, atomically unmark any existing default in the same transaction.
        if (dto.IsDefault)
            await UnmarkExistingDefaultAsync(excluding: calendar.Id, ct);

        await repository.AddAsync(calendar, ct);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<BusinessCalendarReadDto>.Success(mapper.Map<BusinessCalendarReadDto>(calendar));
    }

    public async Task<ServiceResult<BusinessCalendarReadDto>> UpdateAsync(
        Guid id,
        UpdateBusinessCalendarDto dto,
        CancellationToken ct = default)
    {
        var calendar = await repository.GetByIdAsync(id, ct);
        if (calendar is null)
            return ServiceResult<BusinessCalendarReadDto>.NotFound($"BusinessCalendar {id} not found.");

        calendar.Rename(dto.Name);
        await repository.SaveChangesAsync(ct);
        return ServiceResult<BusinessCalendarReadDto>.Success(mapper.Map<BusinessCalendarReadDto>(calendar));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var calendar = await repository.GetByIdAsync(id, ct);
        if (calendar is null) return ServiceResult.NotFound($"BusinessCalendar {id} not found.");

        try
        {
            repository.Remove(calendar);
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ServiceResult.Conflict("BusinessCalendar is referenced by one or more resources.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<WorkWindowDto>> AddWorkWindowAsync(Guid calendarId, WorkWindowDto dto, CancellationToken ct = default)
    {
        // Mutating an OwnsMany collection requires the collection to be loaded with the
        // owner. The generic repository uses DbSet.FindAsync, which only fetches the
        // owner row — it does NOT include owned navigations. Without the original
        // collection state, EF's change tracker can emit a stray UPDATE/DELETE on a
        // shadow row when it tries to "reconcile" the owned table, surfacing as a
        // DbUpdateConcurrencyException (0 rows affected). Loading via a regular tracked
        // query eagerly hydrates OwnsMany collections and avoids the issue. It also
        // means EnsureNoOverlap actually validates against the persisted rows.
        var calendar = await LoadWithWindowsAsync(calendarId, ct);
        if (calendar is null)
            return ServiceResult<WorkWindowDto>.NotFound($"BusinessCalendar {calendarId} not found.");

        var window = WorkWindow.Create(dto.DayOfWeek, dto.StartTime, dto.EndTime, dto.ValidFrom, dto.ValidTo);
        calendar.AddWorkWindow(window);
        db.MarkOwnedAdded(calendar, c => c.WorkWindows, window);

        await repository.SaveChangesAsync(ct);
        return ServiceResult<WorkWindowDto>.Success(mapper.Map<WorkWindowDto>(window));
    }

    public async Task<ServiceResult<Unit>> RemoveWorkWindowAsync(Guid calendarId, Guid windowId, CancellationToken ct = default)
    {
        var calendar = await LoadWithWindowsAsync(calendarId, ct);
        if (calendar is null) return ServiceResult.NotFound($"BusinessCalendar {calendarId} not found.");

        calendar.RemoveWorkWindow(windowId);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private Task<BusinessCalendar?> LoadWithWindowsAsync(Guid id, CancellationToken ct) =>
        db.BusinessCalendars.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<ServiceResult<Unit>> MarkAsDefaultAsync(Guid calendarId, CancellationToken ct = default)
    {
        var calendar = await repository.GetByIdAsync(calendarId, ct);
        if (calendar is null) return ServiceResult.NotFound($"BusinessCalendar {calendarId} not found.");
        if (calendar.IsDefault) return ServiceResult.Ok();

        await UnmarkExistingDefaultAsync(excluding: calendar.Id, ct);
        calendar.MarkAsDefault();
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // Tracks any existing default calendar and clears its flag. The partial unique index
    // enforces the "at most one default" invariant at the DB level; this method keeps the
    // logical change in a single SaveChangesAsync so we don't round-trip a unique violation.
    private async Task UnmarkExistingDefaultAsync(Guid excluding, CancellationToken ct)
    {
        var existing = await db.BusinessCalendars
            .Where(c => c.IsDefault && c.Id != excluding)
            .FirstOrDefaultAsync(ct);
        existing?.UnmarkDefault();
    }
}
