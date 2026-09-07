namespace ResourcePulse.Services.Signals;

// How often the detector is expected to run, so the API can tell the client when
// a pass has gone stale (ADR-0032 §10).
//
// The cadence is OPERATIONAL configuration, not organizational: it belongs to the
// worker's settings, never to SignalPolicy (ADR-0032 §12). This abstraction
// exists so the API — which does not schedule anything — can still echo the
// tolerance instead of every client inventing one.
public interface ISweepCadence
{
    int StaleAfterHours { get; }
}

public sealed class SweepCadence(int staleAfterHours) : ISweepCadence
{
    // Twice the default daily cadence: one missed pass is not yet a fault.
    public const int DefaultStaleAfterHours = 48;

    public int StaleAfterHours { get; } = staleAfterHours > 0 ? staleAfterHours : DefaultStaleAfterHours;
}
