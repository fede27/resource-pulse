using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Resources;

// Owned value object on Resource. Date range is fully inclusive — same convention as
// CompanyClosure, since adjustments are also "events" rather than config boundaries.
// Hours semantics:
//   Absence + null Hours    -> full-day absence (zeros that day's base).
//   Absence + Hours = X     -> subtract X from base (clamped at zero in calculator).
//   ExtraTime + Hours = X   -> add X. Hours is required and must be positive.
public sealed class IndividualAdjustment
{
    public Guid Id { get; private set; }
    public DateOnly DateFrom { get; private set; }
    public DateOnly DateTo { get; private set; }
    public AdjustmentType Type { get; private set; }
    public TimeSpan? Hours { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    private IndividualAdjustment() { }

    public static IndividualAdjustment Create(
        DateOnly dateFrom,
        DateOnly dateTo,
        AdjustmentType type,
        TimeSpan? hours,
        string reason,
        string? notes)
    {
        if (dateFrom > dateTo)
            throw new DomainException("IndividualAdjustment DateFrom must be on or before DateTo.");
        var trimmedReason = (reason ?? string.Empty).Trim();
        if (trimmedReason.Length == 0)
            throw new DomainException("IndividualAdjustment reason must not be empty.");

        if (type == AdjustmentType.ExtraTime)
        {
            if (hours is null)
                throw new DomainException("ExtraTime adjustment requires Hours.");
            if (hours.Value <= TimeSpan.Zero)
                throw new DomainException("ExtraTime adjustment Hours must be positive.");
        }
        else if (hours is not null && hours.Value <= TimeSpan.Zero)
        {
            throw new DomainException("Partial Absence Hours must be positive when provided.");
        }

        return new IndividualAdjustment
        {
            Id = Guid.NewGuid(),
            DateFrom = dateFrom,
            DateTo = dateTo,
            Type = type,
            Hours = hours,
            Reason = trimmedReason,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public bool Covers(DateOnly date) => DateFrom <= date && date <= DateTo;
}
