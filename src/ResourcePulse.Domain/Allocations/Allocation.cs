using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Allocations;

// A resource committed to a project node at a given percentage over an inclusive
// date window. Aggregate root with its own lifecycle. Local invariants only —
// node-type, no-overlap, resource-active, and project-status checks live in the
// service layer (see AllocationService). The DB EXCLUDE constraint backstops I2.
public sealed class Allocation : Entity<Guid>, IAuditable
{
    public Guid ResourceId { get; private set; }
    public Guid ProjectNodeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal AllocationPercent { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Allocation() { }

    public static Allocation Create(
        Guid resourceId,
        Guid projectNodeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        string? notes = null)
    {
        if (resourceId == Guid.Empty)
            throw new DomainException("Allocation must reference a resource.");
        if (projectNodeId == Guid.Empty)
            throw new DomainException("Allocation must reference a project node.");
        if (periodStart > periodEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        AssertPercentInRange(allocationPercent);

        var id = Guid.NewGuid();
        var allocation = new Allocation
        {
            Id = id,
            ResourceId = resourceId,
            ProjectNodeId = projectNodeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AllocationPercent = allocationPercent,
            Notes = NormalizeNotes(notes)
        };

        allocation.RaiseEvent(new AllocationCreated(
            id, resourceId, projectNodeId, periodStart, periodEnd, allocationPercent, DateTimeOffset.UtcNow));
        return allocation;
    }

    public void ChangePeriod(DateOnly newStart, DateOnly newEnd)
    {
        if (newStart > newEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        if (PeriodStart == newStart && PeriodEnd == newEnd) return; // no-op suppresses event

        var oldStart = PeriodStart;
        var oldEnd = PeriodEnd;
        PeriodStart = newStart;
        PeriodEnd = newEnd;
        RaiseEvent(new AllocationPeriodChanged(Id, oldStart, oldEnd, newStart, newEnd, DateTimeOffset.UtcNow));
    }

    public void ChangePercent(decimal newPercent)
    {
        AssertPercentInRange(newPercent);
        if (AllocationPercent == newPercent) return; // no-op suppresses event

        var oldPercent = AllocationPercent;
        AllocationPercent = newPercent;
        RaiseEvent(new AllocationPercentChanged(Id, oldPercent, newPercent, DateTimeOffset.UtcNow));
    }

    public void Annotate(string? notes) => Notes = NormalizeNotes(notes);

    // Called by the service layer immediately before repository.Remove so the
    // AllocationDeleted event is recorded on the aggregate before EF deletes it.
    // Events are scaffolded but not dispatched (Phase 3 convention).
    public void MarkDeleted() =>
        RaiseEvent(new AllocationDeleted(Id, DateTimeOffset.UtcNow));

    private static void AssertPercentInRange(decimal percent)
    {
        if (percent <= 0m || percent > 100m)
            throw new DomainException("AllocationPercent must be in the range (0, 100].");
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is null) return null;
        var trimmed = notes.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
