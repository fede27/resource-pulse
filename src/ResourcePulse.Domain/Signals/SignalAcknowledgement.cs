using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Signals;

public enum AcknowledgementAction
{
    Accept = 0,
    Reopen = 1
}

// One entry in a signal's APPEND-ONLY acknowledgement log (ADR-0032 §4).
//
// Not three fields on the aggregate: the dashboard has a "reopen" button, so the
// accept → reopen → accept cycle is a real sequence, and for an organizational
// decision carried with a written motivation somebody eventually asks who
// accepted it and who reopened it. The current state is derived from the last
// entry; this costs what the three fields cost and throws nothing away.
public sealed class SignalAcknowledgement
{
    // Monotonic within the signal — the ordering key. Timestamps can tie; a
    // sequence cannot, and "the current state is the last entry" must be exact.
    public int Sequence { get; private set; }

    public AcknowledgementAction Action { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string By { get; private set; } = string.Empty;

    // The motivation. Optional on Accept ("Motivo (opzionale)"), never carried on
    // Reopen — reopening restores the default state and needs no justification.
    public string? Reason { get; private set; }

    private SignalAcknowledgement() { }

    internal static SignalAcknowledgement Create(
        int sequence,
        AcknowledgementAction action,
        DateTimeOffset at,
        string by,
        string? reason)
    {
        if (sequence <= 0)
            throw new DomainException("Acknowledgement sequence must be positive.");
        if (!Enum.IsDefined(action))
            throw new DomainException($"Invalid acknowledgement action '{action}'.");

        var author = (by ?? string.Empty).Trim();
        if (author.Length == 0)
            throw new DomainException("An acknowledgement must record its author.");

        var trimmedReason = reason?.Trim();
        if (trimmedReason?.Length == 0) trimmedReason = null;

        return new SignalAcknowledgement
        {
            Sequence = sequence,
            Action = action,
            At = at,
            By = author,
            Reason = action == AcknowledgementAction.Accept ? trimmedReason : null
        };
    }
}
