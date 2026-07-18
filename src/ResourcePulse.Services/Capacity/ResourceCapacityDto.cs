namespace ResourcePulse.Services.Capacity;

// Wire read-model for the batch capacity endpoint (api-roundtrip-consolidation.md
// P1). The daily series is piecewise-constant by construction (work windows +
// closures + adjustments), so the wire form is run-length segments — the same
// choice ADR-0023 made for the commitment profile. Days not covered by any
// segment have zero capacity; a resource with no working days in the range
// still appears, with an empty Segments list.
public sealed class ResourceCapacityDto
{
    public Guid ResourceId { get; init; }

    public IReadOnlyList<CapacitySegmentDto> Segments { get; init; } = [];
}

// Inclusive [From, To] run of days sharing the same daily capacity.
public sealed class CapacitySegmentDto
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public TimeSpan HoursPerDay { get; init; }
}
