using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Projects;

// The third anchor type (ADR-0034 §4, model §5): a date IMPOSED on a project
// from outside — an RFP's desiderata, a contractual deadline of the
// price-taker. The delivery plan must reconcile against it but does not
// control it, which is exactly why it is its own aggregate and not a date on
// the project: several per root (M1..M4 of a tender are four anchors), each
// with a name, and each with an AUTHORITY the plan can read — a customer's
// wish and a penalty clause draw the same line on the board and do not carry
// the same weight (§9, "autorità del confine").
//
// The distance between this date and what the plan really sustains is the
// negotiation surface — the read-model that gives this aggregate its value is
// a later step; the aggregate exists so that it can.
//
// Local invariants: root id non-empty, name non-empty (≤ 200), authority known.
// That the root IS a Project root lives in the service (it needs the node).
public sealed class ExternalConstraint : Entity<Guid>, IAuditable
{
    public Guid RootProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public ConstraintAuthority Authority { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private ExternalConstraint() { }

    public static ExternalConstraint Create(
        Guid rootProjectId, string name, DateOnly date, ConstraintAuthority authority, string? notes = null)
    {
        if (rootProjectId == Guid.Empty)
            throw new DomainException("External constraint must reference a root project.");
        AssertAuthorityKnown(authority);

        var c = new ExternalConstraint
        {
            Id = Guid.NewGuid(),
            RootProjectId = rootProjectId,
            Name = NormalizeName(name),
            Date = date,
            Authority = authority,
            Notes = NormalizeNotes(notes)
        };
        c.RaiseEvent(new ExternalConstraintCreated(c.Id, rootProjectId, date, authority, DateTimeOffset.UtcNow));
        return c;
    }

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeAuthority(ConstraintAuthority authority)
    {
        AssertAuthorityKnown(authority);
        Authority = authority;
    }

    public void Annotate(string? notes) => Notes = NormalizeNotes(notes);

    // The referent moves. Anchored boundaries follow through the envelope's
    // moveConstraint (ADR-0034 §5); the anagrafica refuses this when anything
    // is anchored. No-op suppresses the event.
    public void MoveTo(DateOnly date)
    {
        if (Date == date) return;
        var old = Date;
        Date = date;
        RaiseEvent(new ExternalConstraintMoved(Id, old, date, DateTimeOffset.UtcNow));
    }

    public void MarkDeleted() =>
        RaiseEvent(new ExternalConstraintDeleted(Id, DateTimeOffset.UtcNow));

    private static void AssertAuthorityKnown(ConstraintAuthority authority)
    {
        if (!Enum.IsDefined(authority))
            throw new DomainException($"Invalid constraint authority '{authority}'.");
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("External constraint name must not be empty.");
        if (trimmed.Length > 200)
            throw new DomainException("External constraint name must be 200 characters or fewer.");
        return trimmed;
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is null) return null;
        var trimmed = notes.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

// Why the boundary exists (§9): the same dashed line, a different weight.
public enum ConstraintAuthority
{
    // The customer's wish — an RFP's desired date. Negotiable in principle.
    Desiderata = 0,
    // A contractual commitment — a penalty clause, a legal deadline.
    Contractual = 1
}
