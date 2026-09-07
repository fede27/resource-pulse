namespace ResourcePulse.Domain.Signals;

// Whether a kind produces one row per subject or a single aggregated row
// (ADR-0032 §7). Derived from the kind, never stored.
//
// The line falls where the tier falls: breaches are named individually because
// each one is a violation with a subject; hygiene and slack are aggregated
// because a real tenant produces dozens of occurrences and a queue with a fixed
// budget must stay O(1) — the prototype's own stated principle, which its
// per-occurrence hygiene rows would have broken.
public enum SignalShape
{
    // One live row per subject. Magnitude is a quantity (hours, percentage
    // points over the floor).
    Subject = 0,

    // One live row per kind, with the occurrences carried as an unordered member
    // payload. Magnitude is a CARDINALITY — which is what structurally prevents
    // an aggregate's magnitude from being derived from a utilization rate.
    Aggregate = 1
}
