namespace ResourcePulse.Domain.Allocations;

// Impegno percepito dello stesso blocco di allocazione. Default Tentative
// (ADR-0015): abbassa la soglia di impegno percepita al momento della creazione.
// Hard è un commitment che richiede fondamento sul progetto: l'invariante I6
// (service-level) ammette Hard solo se il Project radice del nodo target ha
// CommitmentLevel in {Committed, Critical}.
public enum AllocationStatus
{
    Tentative = 0,
    Hard = 1
}
