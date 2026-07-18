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

    // Batch twin (api-roundtrip-consolidation.md P1): one round of queries for the
    // whole population — resources+windows, distinct calendars+windows, adjustments,
    // closures ONCE — then the pure CapacityCalculator per resource in memory.
    // Replaces 4×P queries with 4 total. Same range validation as the singular.
    public async Task<ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>> GetForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default)
    {
        if (from > toInclusive)
        {
            return ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = ["'from' must be on or before 'to'."]
            });
        }

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxRangeDays)
        {
            return ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Validation(new Dictionary<string, string[]>
            {
                ["range"] = [$"Date range must not exceed {MaxRangeDays} days (requested {rangeDays})."]
            });
        }

        // null/empty = the active population; explicit ids are honoured regardless
        // of IsActive (a coverage may reference a since-deactivated resource).
        var idFilter = resourceIds is { Count: > 0 } ? resourceIds.Distinct().ToList() : null;

        // 1. Resources with range-filtered owned WorkWindows.
        var resourcesData = await db.Set<Resource>()
            .AsNoTracking()
            .Where(r => idFilter == null ? r.IsActive : idFilter.Contains(r.Id))
            .Select(r => new ResourceShape(
                r.Id,
                r.Name,
                r.BusinessCalendarId,
                r.WorkWindows
                    .Where(w => w.ValidFrom <= toInclusive
                             && (w.ValidTo == null || w.ValidTo > from))
                    .ToList()))
            .ToListAsync(ct);

        if (resourcesData.Count == 0)
        {
            return ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Success(
                new Dictionary<Guid, IReadOnlyList<DailyCapacityDto>>());
        }

        // 2. Distinct calendars with range-filtered owned WorkWindows.
        var calendarIds = resourcesData.Select(r => r.BusinessCalendarId).Distinct().ToList();
        var calendarsData = await db.Set<BusinessCalendar>()
            .AsNoTracking()
            .Where(c => calendarIds.Contains(c.Id))
            .Select(c => new CalendarShape(
                c.Id,
                c.Name,
                c.IsDefault,
                c.WorkWindows
                    .Where(w => w.ValidFrom <= toInclusive
                             && (w.ValidTo == null || w.ValidTo > from))
                    .ToList()))
            .ToListAsync(ct);

        var calendarsById = calendarsData.ToDictionary(
            c => c.Id,
            c => BusinessCalendar.Hydrate(c.Id, c.Name, c.IsDefault, c.Windows));

        // 3. Adjustments in range for the selected resources, keyed by owner.
        var selectedIds = resourcesData.Select(r => r.Id).ToList();
        var adjustmentRows = await db.Set<Resource>()
            .AsNoTracking()
            .Where(r => selectedIds.Contains(r.Id))
            .SelectMany(r => r.Adjustments
                .Where(a => a.DateFrom <= toInclusive && a.DateTo >= from)
                .Select(a => new { ResourceId = r.Id, Adjustment = a }))
            .ToListAsync(ct);

        var adjustmentsByResource = adjustmentRows
            .GroupBy(x => x.ResourceId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Adjustment).ToList());

        // 4. Closures ONCE for the whole batch (they are company-wide).
        var closures = await db.Set<CompanyClosure>()
            .AsNoTracking()
            .Where(c => c.DateFrom <= toInclusive && c.DateTo >= from)
            .ToListAsync(ct);

        var result = new Dictionary<Guid, IReadOnlyList<DailyCapacityDto>>(resourcesData.Count);
        foreach (var r in resourcesData)
        {
            if (!calendarsById.TryGetValue(r.BusinessCalendarId, out var calendar))
            {
                // Should be unreachable thanks to FK Restrict — same integrity
                // stance as the singular read.
                return ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Failure(
                    ServiceError.Failure($"BusinessCalendar {r.BusinessCalendarId} referenced by resource {r.Id} is missing."));
            }

            var resource = Resource.Hydrate(r.Id, r.Name, r.BusinessCalendarId, r.Windows,
                adjustmentsByResource.TryGetValue(r.Id, out var adj) ? adj : []);

            result[r.Id] = CapacityCalculator
                .ForRange(resource, calendar, closures, from, toInclusive)
                .Select(d => new DailyCapacityDto { Date = d.Date, Hours = d.Hours })
                .ToList();
        }

        return ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<ResourceCapacityDto>>> GetSegmentsForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default)
    {
        var daily = await GetForResourcesAsync(resourceIds, from, toInclusive, ct);
        if (daily.IsFailure)
            return ServiceResult<IReadOnlyList<ResourceCapacityDto>>.Failure(daily.Error!);

        var dtos = daily.Value
            .Select(kvp => new ResourceCapacityDto
            {
                ResourceId = kvp.Key,
                Segments = CapacitySegments.Compress(kvp.Value)
            })
            .OrderBy(x => x.ResourceId)
            .ToList();

        return ServiceResult<IReadOnlyList<ResourceCapacityDto>>.Success(dtos);
    }

    private sealed record ResourceShape(Guid Id, string Name, Guid BusinessCalendarId, List<WorkWindow> Windows);
    private sealed record CalendarShape(Guid Id, string Name, bool IsDefault, List<WorkWindow> Windows);
}
