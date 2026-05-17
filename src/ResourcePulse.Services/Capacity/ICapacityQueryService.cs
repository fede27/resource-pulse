using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Capacity;

// Abstraction so future implementations (e.g. SnapshotCapacityQueryService reading from a
// pre-computed table) can be drop-in replacements for LiveCapacityQueryService.
public interface ICapacityQueryService
{
    Task<ServiceResult<IReadOnlyList<DailyCapacityDto>>> GetForResourceAsync(
        Guid resourceId,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);
}
