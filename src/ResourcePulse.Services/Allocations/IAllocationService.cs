using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Allocations;

public interface IAllocationService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);

    // Detail read — populates ResolvedHours (when applicable; null for placeholders).
    Task<ServiceResult<AllocationReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Range-filtered list reads — ResolvedHours is null (see ADR-0013, D1).
    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForResourceAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<AllocationReadDto>>> GetForProjectNodeAsync(
        Guid projectNodeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);

    // Sidecar — cheap per-row hours lookup at current capacity.
    // Returns Conflict if invoked on a placeholder allocation (no resource ⇒ no capacity).
    Task<ServiceResult<AllocationResolvedHoursDto>> GetResolvedHoursAsync(
        Guid id, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> CreateByPercentAsync(
        CreateByPercentDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> CreateByHoursAsync(
        CreateByHoursDto dto, CancellationToken ct = default);

    // Placeholder-creation path (ADR-0016). Solo rate-shaped: senza risorsa
    // non c'è capacity, quindi nessuna conversione hours → percent.
    Task<ServiceResult<AllocationReadDto>> CreatePlaceholderByPercentAsync(
        CreatePlaceholderByPercentDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> UpdateAsync(
        Guid id, UpdateAllocationDto dto, CancellationToken ct = default);

    Task<ServiceResult<AllocationReadDto>> MoveAsync(
        Guid id, MoveAllocationDto dto, CancellationToken ct = default);

    // Assegnato → Placeholder (deallocazione come conversione, ADR-0016).
    Task<ServiceResult<AllocationReadDto>> ConvertToPlaceholderAsync(
        Guid id, ConvertToPlaceholderDto dto, CancellationToken ct = default);

    // Placeholder → Assegnato (ADR-0016).
    Task<ServiceResult<AllocationReadDto>> AssignToResourceAsync(
        Guid id, AssignToResourceDto dto, CancellationToken ct = default);

    // Promozione/demozione status (ADR-0015). I6 si applica anche ai placeholder.
    Task<ServiceResult<AllocationReadDto>> ChangeStatusAsync(
        Guid id, ChangeAllocationStatusDto dto, CancellationToken ct = default);

    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
