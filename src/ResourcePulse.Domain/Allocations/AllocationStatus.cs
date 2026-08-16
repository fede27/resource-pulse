namespace ResourcePulse.Domain.Allocations;

// How committed the same allocation block is. Defaults to Tentative
// (ADR-0015): it lowers the perceived commitment at the moment of creation.
// Hard is a commitment that has to be grounded in the project: invariant I6
// (service-level) admits Hard only if the root Project of the target node has
// CommitmentLevel in {Committed, Critical}.
public enum AllocationStatus
{
    Tentative = 0,
    Hard = 1
}
