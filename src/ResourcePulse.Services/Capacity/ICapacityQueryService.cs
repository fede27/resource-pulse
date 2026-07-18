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

    // Batch twin of GetForResourceAsync (api-roundtrip-consolidation.md P1): one
    // round of queries for a whole population instead of 4 per resource.
    // `resourceIds` null or empty = all active resources; explicitly requested
    // ids are honoured regardless of IsActive; unknown ids are simply absent
    // from the result (a batch read has no per-id NotFound).
    Task<ServiceResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyCapacityDto>>>> GetForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);

    // Wire read-model for GET /api/resources/capacity: the same batch, compressed
    // to run-length segments (the daily series is piecewise-constant). Lives on
    // the interface so a snapshot implementation can serve segments natively.
    Task<ServiceResult<IReadOnlyList<ResourceCapacityDto>>> GetSegmentsForResourcesAsync(
        IReadOnlyCollection<Guid>? resourceIds,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken ct = default);
}
