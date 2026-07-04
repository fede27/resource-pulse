using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Demands;

// READ side only (Phase 5.0). Demand mutation lives behind the command envelope
// (IPlanCommandService, ADR-0018): createDemand / editDemand / deleteDemand and
// the coverInferred materialization.
public interface IDemandService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);

    Task<ServiceResult<DemandReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Subtree aggregation (ADR-0022): the node + every descendant via the
    // materialized-path prefix. `from`/`to` are accepted for symmetry with the
    // allocation reads; the scalar demand itself is range-independent (revision
    // §4 / Decision 4), so today they are not used to filter — reserved for the
    // future time-distributed demand.
    Task<ServiceResult<IReadOnlyList<DemandReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, CancellationToken ct = default);
}
