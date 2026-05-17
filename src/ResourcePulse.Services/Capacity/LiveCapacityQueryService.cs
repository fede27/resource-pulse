using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Capacity;

// Loads only the windows/closures/adjustments relevant to the requested date range,
// then runs the pure CapacityCalculator on the filtered inputs. Four separate queries
// rather than a single Include chain because EF Core does not cleanly filter multiple
// owned collections in one query (per Phase 2 plan addendum).
public sealed class LiveCapacityQueryService(ResourcePulseDbContext db) : ICapacityQueryService
{
    private const int MaxRangeDays = 366;

    public async Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default)
    {
        if (from > toInclusive)
        {
            return ServiceResult<IReadOnlyList<DailyCapacityDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });
        }

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
        {
            return ServiceResult<IReadOnlyList<DailyCapacityDto>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });
        }

        // 1. Resource with filtered owned WorkWindows.
        var resourceData = await db.Set<Resource>()
            .AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new ResourceShape(
                r.Id,
                r.Name,
                r.BusinessCalendarId,
                r.WorkWindows
                    .Where(w => w.ValidFrom <= toInclusive
                             && (w.ValidTo == null || w.ValidTo > from))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (resourceData is null)
            return ServiceResult<IReadOnlyList<DailyCapacityDto>>.NotFound($"Resource {resourceId} not found.");

        // 2. BusinessCalendar with filtered owned WorkWindows.
        var calendarData = await db.Set<BusinessCalendar>()
            .AsNoTracking()
            .Where(c => c.Id == resourceData.BusinessCalendarId)
            .Select(c => new CalendarShape(
                c.Id,
                c.Name,
                c.IsDefault,
                c.WorkWindows
                    .Where(w => w.ValidFrom <= toInclusive
                             && (w.ValidTo == null || w.ValidTo > from))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (calendarData is null)
        {
            // Should be unreachable thanks to FK Restrict, but treat as data integrity error.
            return ServiceResult<IReadOnlyList<DailyCapacityDto>>.Failure(
                ServiceError.Failure($"BusinessCalendar {resourceData.BusinessCalendarId} referenced by resource is missing."));
        }

        // 3. Adjustments filtered by range (date-range overlap with [from, toInclusive]).
        var adjustments = await db.Set<Resource>()
            .AsNoTracking()
            .Where(r => r.Id == resourceId)
            .SelectMany(r => r.Adjustments)
            .Where(a => a.DateFrom <= toInclusive && a.DateTo >= from)
            .ToListAsync(ct);

        // 4. Closures filtered by range.
        var closures = await db.Set<CompanyClosure>()
            .AsNoTracking()
            .Where(c => c.DateFrom <= toInclusive && c.DateTo >= from)
            .ToListAsync(ct);

        var calendar = BusinessCalendar.Hydrate(calendarData.Id, calendarData.Name, calendarData.IsDefault, calendarData.Windows);
        var resource = Resource.Hydrate(resourceData.Id, resourceData.Name, resourceData.BusinessCalendarId,
            resourceData.Windows, adjustments);

        var result = CapacityCalculator
            .ForRange(resource, calendar, closures, from, toInclusive)
            .Select(d => new DailyCapacityDto { Date = d.Date, Hours = d.Hours })
            .ToList();

        return ServiceResult<IReadOnlyList<DailyCapacityDto>>.Success(result);
    }

    private sealed record ResourceShape(Guid Id, string Name, Guid BusinessCalendarId, List<WorkWindow> Windows);
    private sealed record CalendarShape(Guid Id, string Name, bool IsDefault, List<WorkWindow> Windows);
}
