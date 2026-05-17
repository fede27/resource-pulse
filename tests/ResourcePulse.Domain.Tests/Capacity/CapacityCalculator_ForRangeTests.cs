namespace ResourcePulse.Domain.Tests.Capacity;

public class CapacityCalculator_ForRangeTests
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Fact]
    public void Range_EmptyWhenFromAfterTo()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 1)).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Range_SingleDay_YieldsOneEntry()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1)).ToList();

        result.Should().ContainSingle();
        result[0].Date.Should().Be(new DateOnly(2026, 6, 1));
        result[0].Hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Range_SpansWeekBoundary_WeekendsZero()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        // Mon 2026-06-01 .. Sun 2026-06-07
        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7)).ToList();

        result.Should().HaveCount(7);
        result.Where(d => d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .Should().OnlyContain(d => d.Hours == TimeSpan.Zero);
        result.Where(d => d.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Should().OnlyContain(d => d.Hours == TimeSpan.FromHours(8));
    }

    [Fact]
    public void Range_WithMidRangeClosure_ZeroOnlyOnClosureDays()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();
        var closures = new[] { CompanyClosureBuilder.SingleDay("2026-06-03") }; // Wed

        var result = CapacityCalculator.ForRange(
            resource, calendar, closures, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)).ToList();

        result.Should().HaveCount(5);
        result.Single(d => d.Date == new DateOnly(2026, 6, 3)).Hours.Should().Be(TimeSpan.Zero);
        result.Where(d => d.Date != new DateOnly(2026, 6, 3))
            .Should().OnlyContain(d => d.Hours == TimeSpan.FromHours(8));
    }

    [Fact]
    public void Range_WithAdjustmentOverlappingStart_AppliesOnCoveredDays()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithFullDayAbsence("2026-05-28", "2026-06-02") // overlaps before & into the range
            .Build();

        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)).ToList();

        result.Single(d => d.Date == new DateOnly(2026, 6, 1)).Hours.Should().Be(TimeSpan.Zero);
        result.Single(d => d.Date == new DateOnly(2026, 6, 2)).Hours.Should().Be(TimeSpan.Zero);
        result.Single(d => d.Date == new DateOnly(2026, 6, 3)).Hours.Should().Be(TimeSpan.FromHours(8));
        result.Single(d => d.Date == new DateOnly(2026, 6, 5)).Hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Range_CrossesMonthAndYearBoundary()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        // 2026-12-30 (Wed) .. 2027-01-02 (Sat) — 4 days, Wed/Thu/Fri = 8h each, Sat = 0
        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 2)).ToList();

        result.Select(d => d.Date).Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 12, 30),
            new DateOnly(2026, 12, 31),
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 2),
        }, opts => opts.WithStrictOrdering());

        result.Single(d => d.Date == new DateOnly(2027, 1, 2)).Hours.Should().Be(TimeSpan.Zero); // Sat
        result.Where(d => d.Date != new DateOnly(2027, 1, 2))
            .Should().OnlyContain(d => d.Hours == TimeSpan.FromHours(8));
    }
}
