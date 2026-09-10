using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Application.Tests;

// The guard every ranged read runs before it reads. It had eleven copies and no
// test; now it has one copy, so the edges are worth stating once.
public class DateRangeGuardTests
{
    private static readonly DateOnly Day = new(2026, 1, 1);

    [Fact]
    public void TheCapIsTheCommittingHorizon()
    {
        // TimeFenceConfiguration refuses a horizon longer than it can be measured
        // on, and this is the read that measures it. Two constants that merely
        // happened to both say 366 could drift; this one cannot.
        DateRangeGuard.MaxDays.Should().Be(TimeFenceConfiguration.MaxHorizonDays);
    }

    [Fact]
    public void AUsableRangePassesBothChecks()
    {
        DateRangeGuard.Validate(Day, Day).Should().BeNull();
        DateRangeGuard.Validate(Day, Day.AddDays(30)).Should().BeNull();
    }

    [Fact]
    public void AnInvertedRangeIsRefused()
    {
        var error = DateRangeGuard.Validate(Day.AddDays(1), Day);

        error.Should().NotBeNull();
        error!.Kind.Should().Be(ServiceErrorKind.Validation);
        error.Details!["range"].Single().Should().Be("'from' must be on or before 'to'.");
    }

    [Fact]
    public void TheCapCountsBothEndpoints()
    {
        // The range is inclusive, so the widest legal one is MaxDays - 1 apart.
        DateRangeGuard.Validate(Day, Day.AddDays(DateRangeGuard.MaxDays - 1)).Should().BeNull();

        var error = DateRangeGuard.Validate(Day, Day.AddDays(DateRangeGuard.MaxDays));

        error.Should().NotBeNull();
        error!.Details!["range"].Single().Should()
            .Be($"Date range must not exceed {DateRangeGuard.MaxDays} days (requested {DateRangeGuard.MaxDays + 1}).");
    }

    [Fact]
    public void OrderingOnlyStillRefusesInversion_ButAcceptsALongRange()
    {
        DateRangeGuard.ValidateOrdering(Day.AddDays(1), Day).Should().NotBeNull();
        DateRangeGuard.ValidateOrdering(Day, Day.AddYears(10)).Should().BeNull();
    }

    [Fact]
    public void BothChecksReportUnderTheSameKey()
    {
        // One key, so a client that surfaces field errors does not have to know
        // which of the two questions the range failed. It used to be "from" on
        // the projects listing and "range" everywhere else.
        DateRangeGuard.Validate(Day.AddDays(1), Day)!.Details!.Keys.Should().Equal("range");
        DateRangeGuard.Validate(Day, Day.AddDays(DateRangeGuard.MaxDays))!.Details!.Keys.Should().Equal("range");
    }
}
