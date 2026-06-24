using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Configuration;

// Owned value object: a horizon length expressed as a value + unit (ADR-0020).
// Stored as a duration, never as a date — the time fence is rolling-from-today,
// so the boundary date is recomputed at every read (§5 time fence).
public sealed class Duration
{
    public int Value { get; private set; }
    public DurationUnit Unit { get; private set; }

    private Duration() { }

    public static Duration Of(int value, DurationUnit unit)
    {
        if (value <= 0)
            throw new DomainException("Duration value must be a positive integer.");
        if (!Enum.IsDefined(unit))
            throw new DomainException($"Invalid duration unit '{unit}'.");

        return new Duration { Value = value, Unit = unit };
    }

    // Projects the rolling horizon onto a concrete date using calendar arithmetic.
    public DateOnly AddTo(DateOnly date) => Unit switch
    {
        DurationUnit.Days => date.AddDays(Value),
        DurationUnit.Weeks => date.AddDays(Value * 7),
        DurationUnit.Months => date.AddMonths(Value),
        _ => throw new DomainException($"Invalid duration unit '{Unit}'.")
    };

    // Comparison basis for validating one horizon against another, independent of
    // any "today". Months use a 30-day approximation — this is the documented
    // CONSTANT used only for ordering (frozen < slushy), not for boundary dates.
    public int ApproximateDays => Unit switch
    {
        DurationUnit.Days => Value,
        DurationUnit.Weeks => Value * 7,
        DurationUnit.Months => Value * 30,
        _ => throw new DomainException($"Invalid duration unit '{Unit}'.")
    };
}
