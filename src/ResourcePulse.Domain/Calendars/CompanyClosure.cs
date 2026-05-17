using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Calendars;

// Global closure (holiday, shutdown). Affects all resources on the covered dates.
// Date range is FULLY INCLUSIVE on both ends — diverges from WorkWindow's half-open
// validity because closures are events, not config boundaries.
public sealed class CompanyClosure : Entity<Guid>, IAuditable
{
    public DateOnly DateFrom { get; private set; }
    public DateOnly DateTo { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private CompanyClosure() { }

    public static CompanyClosure Create(DateOnly dateFrom, DateOnly dateTo, string reason)
    {
        if (dateFrom > dateTo)
            throw new DomainException("CompanyClosure DateFrom must be on or before DateTo.");

        var trimmed = (reason ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("CompanyClosure reason must not be empty.");

        return new CompanyClosure
        {
            Id = Guid.NewGuid(),
            DateFrom = dateFrom,
            DateTo = dateTo,
            Reason = trimmed
        };
    }

    public void Update(DateOnly dateFrom, DateOnly dateTo, string reason)
    {
        if (dateFrom > dateTo)
            throw new DomainException("CompanyClosure DateFrom must be on or before DateTo.");
        var trimmed = (reason ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("CompanyClosure reason must not be empty.");

        DateFrom = dateFrom;
        DateTo = dateTo;
        Reason = trimmed;
    }

    public bool Covers(DateOnly date) => DateFrom <= date && date <= DateTo;
}
