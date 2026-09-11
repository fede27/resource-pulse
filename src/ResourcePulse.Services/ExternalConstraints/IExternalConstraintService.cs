using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.ExternalConstraints;

// Imposed dates on a root project (ADR-0034 §4). Anagrafica: create, rename,
// re-weigh, annotate, delete. MOVING the date is a plan mutation once anything
// is anchored to it — that is the envelope's moveConstraint, and UpdateAsync
// refuses it with the count.
public interface IExternalConstraintService
{
    Task<ServiceResult<IReadOnlyList<ExternalConstraintReadDto>>> GetForRootAsync(Guid rootProjectId, CancellationToken ct = default);
    Task<ServiceResult<ExternalConstraintReadDto>> GetByIdAsync(Guid rootProjectId, Guid id, CancellationToken ct = default);
    Task<ServiceResult<ExternalConstraintReadDto>> CreateAsync(Guid rootProjectId, CreateExternalConstraintDto dto, CancellationToken ct = default);
    Task<ServiceResult<ExternalConstraintReadDto>> UpdateAsync(Guid rootProjectId, Guid id, UpdateExternalConstraintDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid rootProjectId, Guid id, CancellationToken ct = default);
}
