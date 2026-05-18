namespace ResourcePulse.Domain.Projects;

// Governs how a Project/Phase is planned for capacity purposes:
//   Unspecified   — neither work nor duration is fixed (default for new nodes).
//   FixedWork     — total effort (EstimatedWork) is fixed; duration flexes.
//   FixedDuration — the date window is fixed; work flexes via allocations.
//
// PlanningMode is orienting metadata only; it does not change the schema of
// Allocation or alter calculator behavior. See ADR-0009.
public enum PlanningMode
{
    Unspecified = 0,
    FixedWork,
    FixedDuration
}
