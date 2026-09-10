using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Services.Shared;

/// <summary>
/// The two questions every ranged read asks before it reads anything: is the
/// range the right way round, and is it short enough to answer.
/// </summary>
/// <remarks>
/// <para>
/// The cap IS <see cref="TimeFenceConfiguration.MaxHorizonDays"/>, not a number
/// that happens to equal it. That constant already carries the note that the
/// committing horizon, the read-model cap and the detector's chunk size "must
/// agree"; with eleven private copies of <c>366</c> spread over three services,
/// agreeing was something a person had to keep doing. Now it is one edit.
/// </para>
/// <para>
/// Returns a <see cref="ServiceError"/> rather than a
/// <c>ServiceResult&lt;T&gt;</c> so the caller keeps its own return type without
/// having to name it a second time as a type argument.
/// </para>
/// </remarks>
public static class DateRangeGuard
{
    public const int MaxDays = TimeFenceConfiguration.MaxHorizonDays;

    /// <summary>Ordering and the read-model cap. Null when the range is usable.</summary>
    public static ServiceError? Validate(DateOnly from, DateOnly toInclusive)
    {
        if (ValidateOrdering(from, toInclusive) is { } ordering)
            return ordering;

        var rangeDays = toInclusive.DayNumber - from.DayNumber + 1;
        if (rangeDays > MaxDays)
            return Refuse($"Date range must not exceed {MaxDays} days (requested {rangeDays}).");

        return null;
    }

    /// <summary>
    /// Ordering only, for the reads that are bounded by something other than the
    /// span of the range — a single resource's coverage, one project's dates.
    /// Deliberately a separate method: "no cap here" should be visible at the call
    /// site, not inferred from an argument nobody passed.
    /// </summary>
    public static ServiceError? ValidateOrdering(DateOnly from, DateOnly toInclusive) =>
        from > toInclusive ? Refuse("'from' must be on or before 'to'.") : null;

    private static ServiceError Refuse(string message) =>
        ServiceError.Validation(new Dictionary<string, string[]> { ["range"] = [message] });
}
