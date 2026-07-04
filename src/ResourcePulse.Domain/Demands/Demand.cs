using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Demands;

// A unit of demand: "this work needs a role, for this many hours" (revision §2,
// §4). First-class aggregate, NOT an attribute of the project — a project has a
// need, not a capacity. The demand is per-role and lives on a ProjectNode
// (Project or Phase); the capacity-planning-level rule (I1) is enforced at the
// service layer, like allocations.
//
// Uncovered demand is the native "to be staffed" state (revision §8): the old
// placeholder-Allocation is re-read as a Demand with no coverage on it. Coverage
// (the Allocation aggregate) always references a real resource and points at a
// demand; the role lives here, on the demand, never on the resource or the
// coverage (§6). A "storto" match (a resource whose role differs from the
// demand's role) is surfaced, not enforced — there is deliberately no invariant
// tying a covering resource's role to this RoleId.
//
// Units (ADR-0026): RequiredHours is an amount of effort, carried as TimeSpan —
// the same single representation as capacity, resolved coverage hours and the
// gap. When null, the demand is best-effort: it exists and can be covered, but
// there is no target and therefore no defined gap (revision §7).
//
// Local invariants only:
//   - ProjectNodeId, RoleId not empty
//   - RequiredHours, when present, strictly > 0
//   - OwnerResourceId, when provided, not empty
//
// Cross-aggregate (I1 planning-level node, I4 root not Closed/Cancelled) live in
// the service layer, mirroring the allocation guards.
public sealed class Demand : Entity<Guid>, IAuditable
{
    public Guid ProjectNodeId { get; private set; }
    public Guid RoleId { get; private set; }

    // Null => best-effort (no target, no gap — revision §7). When present, > 0.
    public TimeSpan? RequiredHours { get; private set; }

    public DemandProvenance Provenance { get; private set; }

    // Who owns filling the gap (revision §4/§8): the PM or the staffing manager.
    // Optional; visibility without an owner is ignorable noise (alarm-fatigue).
    public Guid? OwnerResourceId { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsBestEffort => RequiredHours is null;

    private Demand() { }

    public static Demand Create(
        Guid projectNodeId,
        Guid roleId,
        TimeSpan? requiredHours,
        DemandProvenance provenance,
        Guid? ownerResourceId = null,
        string? notes = null)
    {
        if (projectNodeId == Guid.Empty)
            throw new DomainException("Demand must reference a project node.");
        if (roleId == Guid.Empty)
            throw new DomainException("Demand must reference a role.");
        AssertRequiredHours(requiredHours);
        AssertOwner(ownerResourceId);
        AssertProvenanceKnown(provenance);

        var id = Guid.NewGuid();
        var demand = new Demand
        {
            Id = id,
            ProjectNodeId = projectNodeId,
            RoleId = roleId,
            RequiredHours = requiredHours,
            Provenance = provenance,
            OwnerResourceId = ownerResourceId,
            Notes = NormalizeNotes(notes)
        };

        demand.RaiseEvent(new DemandCreated(
            id, projectNodeId, roleId, requiredHours, provenance, ownerResourceId, DateTimeOffset.UtcNow));
        return demand;
    }

    // Set or clear the target. Clearing (null) moves the demand to best-effort.
    public void ChangeRequiredHours(TimeSpan? requiredHours)
    {
        AssertRequiredHours(requiredHours);
        if (RequiredHours == requiredHours) return; // no-op suppresses event

        var old = RequiredHours;
        RequiredHours = requiredHours;
        RaiseEvent(new DemandRequiredHoursChanged(Id, old, requiredHours, DateTimeOffset.UtcNow));
    }

    public void ChangeOwner(Guid? ownerResourceId)
    {
        AssertOwner(ownerResourceId);
        if (OwnerResourceId == ownerResourceId) return; // no-op suppresses event

        var old = OwnerResourceId;
        OwnerResourceId = ownerResourceId;
        RaiseEvent(new DemandOwnerChanged(Id, old, ownerResourceId, DateTimeOffset.UtcNow));
    }

    // Correct the demand's role (amendment C2). Does NOT touch existing coverage:
    // if after the correction a covering resource's role differs from RoleId, that
    // is the visible mismatch of §6, not an error. ProjectNodeId is NOT mutable —
    // a demand on another node is another demand (delete + recreate).
    public void ChangeRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
            throw new DomainException("Demand must reference a role.");
        if (RoleId == roleId) return; // no-op suppresses event

        var old = RoleId;
        RoleId = roleId;
        RaiseEvent(new DemandRoleChanged(Id, old, roleId, DateTimeOffset.UtcNow));
    }

    // Notes are bookkeeping: no event, matching Allocation.Annotate.
    public void Annotate(string? notes) => Notes = NormalizeNotes(notes);

    // Called by the service layer just before repository.Remove (ADR-0012 style).
    public void MarkDeleted() => RaiseEvent(new DemandDeleted(Id, DateTimeOffset.UtcNow));

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void AssertRequiredHours(TimeSpan? requiredHours)
    {
        if (requiredHours is TimeSpan h && h <= TimeSpan.Zero)
            throw new DomainException("RequiredHours, when present, must be greater than zero.");
    }

    private static void AssertOwner(Guid? ownerResourceId)
    {
        if (ownerResourceId is Guid o && o == Guid.Empty)
            throw new DomainException("OwnerResourceId, when provided, must not be Guid.Empty.");
    }

    private static void AssertProvenanceKnown(DemandProvenance provenance)
    {
        if (!Enum.IsDefined(provenance))
            throw new DomainException($"Invalid demand provenance '{provenance}'.");
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is null) return null;
        var trimmed = notes.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
