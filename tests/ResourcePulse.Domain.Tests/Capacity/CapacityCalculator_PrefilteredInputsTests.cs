namespace ResourcePulse.Domain.Tests.Capacity;

// Per spec addendum: the calculator trusts pre-filtered inputs and never re-filters.
// These tests assert that behavior directly.
public class CapacityCalculator_PrefilteredInputsTests
{
    [Fact]
    public void EmptyWindowsForDayThatWouldOtherwiseMatch_ReturnsZero()
    {
        // Calendar has no windows at all (simulating query-side filtering removing them).
        // Calculator must not "discover" any pattern — it returns zero.
        var calendar = new BusinessCalendarBuilder().Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AdjustmentExtendingBeyondRange_AppliesOnlyOnCoveredDates()
    {
        // Adjustment spans 2026-06-01..2026-06-10. Calculator is asked for 2026-06-05..2026-06-15.
        // The calculator never sees the date range — it just applies Covers(date) per date.
        // So 06-05..06-10 zeroed, 06-11..06-15 normal.
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "17:00")
            .WithWindow(DayOfWeek.Tuesday, "09:00", "17:00")
            .WithWindow(DayOfWeek.Wednesday, "09:00", "17:00")
            .WithWindow(DayOfWeek.Thursday, "09:00", "17:00")
            .WithWindow(DayOfWeek.Friday, "09:00", "17:00")
            .Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithFullDayAbsence("2026-06-01", "2026-06-10")
            .Build();

        var result = CapacityCalculator.ForRange(
            resource, calendar, [], new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 15)).ToList();

        // 06-05 Fri, 06-08 Mon, 06-09 Tue, 06-10 Wed all covered by absence -> 0
        result.Single(d => d.Date == new DateOnly(2026, 6, 5)).Hours.Should().Be(TimeSpan.Zero);
        result.Single(d => d.Date == new DateOnly(2026, 6, 8)).Hours.Should().Be(TimeSpan.Zero);
        result.Single(d => d.Date == new DateOnly(2026, 6, 10)).Hours.Should().Be(TimeSpan.Zero);

        // 06-11 Thu, 06-12 Fri, 06-15 Mon -> 8h (outside absence)
        result.Single(d => d.Date == new DateOnly(2026, 6, 11)).Hours.Should().Be(TimeSpan.FromHours(8));
        result.Single(d => d.Date == new DateOnly(2026, 6, 12)).Hours.Should().Be(TimeSpan.FromHours(8));
        result.Single(d => d.Date == new DateOnly(2026, 6, 15)).Hours.Should().Be(TimeSpan.FromHours(8));
    }
}
