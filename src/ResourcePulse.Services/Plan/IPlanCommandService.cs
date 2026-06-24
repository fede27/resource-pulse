using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Plan;

// Single dispatch point for plan mutation (ADR-0018). Routes on the runtime
// command type, applies the domain kernel (ADR-0019), and either commits or —
// when DryRun — returns the computed consequence without persisting.
public interface IPlanCommandService
{
    Task<ServiceResult<PlanCommandResult>> ExecuteAsync(PlanCommand command, CancellationToken ct = default);
}
