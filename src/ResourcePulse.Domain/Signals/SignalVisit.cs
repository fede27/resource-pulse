using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Signals;

// When a user last looked at the triage queue (ADR-0032 §5).
//
// This is the ONE piece of dashboard state that is NOT domain data. The
// acknowledgement is the tenant's — authored, motivated, kept. "I have already
// looked" is yours: no motivation, no history, disposable.
//
// Keeping them apart is what avoids a row per (user, signal): one LastVisitedAt
// per user is enough for both the unseen dot (FirstDetectedAt > LastVisitedAt)
// and the "dal …" heading of the change feed. One row per user instead of N.
//
// Tenant-scoped even though it is per-user: the same person can belong to several
// tenants and their reading of one says nothing about the others.
public sealed class SignalVisit : Entity<Guid>, IAuditable
{
    // The authenticated subject, not a Resource: most people who sign in are not
    // planned resources, and most planned resources never sign in (ADR-0029 §7).
    public string UserSub { get; private set; } = string.Empty;

    public DateTimeOffset LastVisitedAt { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private SignalVisit() { }

    public static SignalVisit Create(string userSub, DateTimeOffset visitedAt)
    {
        var sub = (userSub ?? string.Empty).Trim();
        if (sub.Length == 0)
            throw new DomainException("A signal visit must reference an authenticated subject.");

        return new SignalVisit { Id = Guid.NewGuid(), UserSub = sub, LastVisitedAt = visitedAt };
    }

    // Monotonic: a stale request must never move the marker backwards and
    // resurrect a batch of "unseen" rows the user has already read.
    public void Touch(DateTimeOffset visitedAt)
    {
        if (visitedAt > LastVisitedAt) LastVisitedAt = visitedAt;
    }
}
