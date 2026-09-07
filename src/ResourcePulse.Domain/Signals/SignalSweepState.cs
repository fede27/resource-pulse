using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Signals;

// Per-tenant singleton recording when the detector last ran (ADR-0032 §10).
//
// It exists because an EMPTY plan_signals table is ambiguous, and the dashboard
// makes a claim it can only honestly make if it knows it looked: "la coda è
// vuota perché il piano tiene". Without this, that sentence is indistinguishable
// from "the sweep never ran on this tenant" and from "the sweep has been stuck
// for six days" — and on a brand-new tenant the first reading is never the true
// one.
//
// Machine-written, so it is NOT part of SignalPolicy: that is Owner-editable
// configuration, this is the detector's own bookkeeping. Separate lifecycles,
// separate aggregates (ADR-0020's no-mega-blob rule).
public sealed class SignalSweepState : Entity<Guid>, IAuditable
{
    // One row per tenant, enforced by a unique index on tenant_id (ADR-0029).
    // Null until the first sweep completes — that null IS the "non ho ancora
    // guardato" state the page renders.
    public DateTimeOffset? LastSweptAt { get; private set; }

    // How many live signals the last pass left behind. Lets the page tell
    // "healthy" from "stale" without a second query.
    public int LastLiveCount { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private SignalSweepState() { }

    public static SignalSweepState CreateUnswept() => new() { Id = Guid.NewGuid() };

    public void RecordSweep(DateTimeOffset at, int liveCount)
    {
        if (liveCount < 0)
            throw new DomainException("Live signal count must not be negative.");

        LastSweptAt = at;
        LastLiveCount = liveCount;
    }

    // Whether the last pass is old enough that the page should say so rather than
    // present the queue as current. Deliberately a question the caller asks with
    // its own tolerance — this aggregate does not own the cadence, the worker's
    // configuration does.
    public bool IsStale(DateTimeOffset now, TimeSpan tolerance) =>
        LastSweptAt is DateTimeOffset last && now - last > tolerance;
}
