using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Domain.Tests.Builders;

public static class CompanyClosureBuilder
{
    public static CompanyClosure SingleDay(string date, string reason = "Holiday") =>
        CompanyClosure.Create(DateOnly.Parse(date), DateOnly.Parse(date), reason);

    public static CompanyClosure Range(string from, string to, string reason = "Shutdown") =>
        CompanyClosure.Create(DateOnly.Parse(from), DateOnly.Parse(to), reason);
}
