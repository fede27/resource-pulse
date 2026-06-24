namespace ResourcePulse.Domain.Events;

// Raised on Allocation.SplitAt(...) on the source block (which becomes the
// first, earlier block). Structural, non-destructive provenance (ADR-0017):
// the span is cut at SplitDate into [start, SplitDate-1] (the source, same Id)
// and [SplitDate, end] (NewBlockId — a freshly created sibling with identical
// rate%, status, project node and form). The per-day rate% sum is invariant
// because the two blocks do not overlap at the boundary (ADR-0014).
//
// Reason is the decision-level provenance hook, mirroring AllocationStatusChanged
// (ADR-0015): "Split" for a bare split, "ChangeRateFrom" when the split is the
// first half of a mid-span rate change.
//
// The second block carries no creation event of its own: its provenance is this
// AllocationSplit on the source block (same convention as placeholder creation
// in ADR-0016).
public sealed record AllocationSplit(
    Guid SourceAllocationId,
    DateOnly SplitDate,
    Guid NewBlockId,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
