namespace ResourcePulse.Domain.Signals;

// The DETECTOR's axis of the lifecycle (ADR-0032 §3). Deliberately NOT a linear
// state machine with an "acknowledged" step in the middle: acknowledgement is an
// orthogonal, human axis, and an acknowledged signal stays in the queue.
//
// Resolved is written ONLY by the detector. The dashboard's inline "confirm
// allocation" sends a changeStatus to the plan envelope and mutates the plan; the
// next detector pass observes the condition is gone. If the UI could also
// resolve, there would be two writers of the same truth.
public enum SignalDetection
{
    Live = 0,
    Resolved = 1
}
