namespace ResourcePulse.Domain.Tests.Capacity;

public class CapacityCalculator_ForDateTests
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Fact]
    public void Pattern_InheritsCalendar_WhenResourceHasNoOverride()
    {
        var calendar = new BusinessCalendarBuilder()
            .WithWindowOnDays(Weekdays, "09:00", "17:00")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Pattern_UsesResourceOverride_WhenPresent()
    {
        var calendar = new BusinessCalendarBuilder()
            .WithWindowOnDays(Weekdays, "09:00", "17:00")
            .Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .OverrideWindow(DayOfWeek.Monday, "10:00", "14:00")
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void Pattern_Override_OnlyAffectsOverriddenDays()
    {
        // Override Mon only — Tue should fall back to... nothing, because override is non-empty.
        // Spec: if R.WorkWindows is non-empty, use it exclusively. So Tue = 0.
        var calendar = new BusinessCalendarBuilder()
            .WithWindowOnDays(Weekdays, "09:00", "17:00")
            .Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .OverrideWindow(DayOfWeek.Monday, "10:00", "14:00")
            .Build();

        var tueHours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 2)); // Tue

        tueHours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Pattern_EmptyOnGivenDay_ReturnsZero()
    {
        // Calendar has Mon-Fri only; Saturday returns 0.
        var calendar = new BusinessCalendarBuilder()
            .WithWindowOnDays(Weekdays, "09:00", "17:00")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var satHours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 6)); // Sat

        satHours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Pattern_MultipleWindowsSameDay_SumsDurations()
    {
        // Classic lunch break: Mon 09:00-13:00 + Mon 14:00-18:00 = 8h
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "13:00")
            .WithWindow(DayOfWeek.Monday, "14:00", "18:00")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Validity_FutureValidFrom_WindowNotApplied()
    {
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "17:00", validFrom: "2027-01-01")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Validity_PastValidTo_WindowNotApplied()
    {
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "17:00", validFrom: "2020-01-01", validTo: "2024-01-01")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Validity_OnValidFromBoundary_WindowApplies()
    {
        // ValidFrom is INCLUSIVE
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "17:00", validFrom: "2026-06-01")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Validity_OnValidToBoundary_WindowDoesNotApply()
    {
        // ValidTo is EXCLUSIVE (half-open right)
        var calendar = new BusinessCalendarBuilder()
            .WithWindow(DayOfWeek.Monday, "09:00", "17:00", validFrom: "2020-01-01", validTo: "2026-06-01")
            .Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1)); // Mon

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Closure_OnDateInside_ZerosBase()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();
        var closures = new[] { CompanyClosureBuilder.Range("2026-06-15", "2026-06-17") };

        var hours = CapacityCalculator.ForDate(resource, calendar, closures, new DateOnly(2026, 6, 16));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Closure_OnDateFromBoundary_Inclusive()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();
        var closures = new[] { CompanyClosureBuilder.Range("2026-06-15", "2026-06-17") };

        var hours = CapacityCalculator.ForDate(resource, calendar, closures, new DateOnly(2026, 6, 15));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Closure_OnDateToBoundary_Inclusive()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder().OnCalendar(calendar).Build();
        var closures = new[] { CompanyClosureBuilder.Range("2026-06-15", "2026-06-17") };

        var hours = CapacityCalculator.ForDate(resource, calendar, closures, new DateOnly(2026, 6, 17));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Absence_FullDay_ZerosCapacity()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithFullDayAbsence("2026-06-01", "2026-06-01")
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Absence_HalfDay_SubtractsHours()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build(); // 8h
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(4))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void Absence_ExceedingBase_ClampsAtZero()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "13:00").Build(); // 4h
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(10))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ExtraTime_AddsToBase()
    {
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithExtra("2026-06-01", "2026-06-01", TimeSpan.FromHours(2))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.FromHours(10));
    }

    [Fact]
    public void ExtraTime_OnClosureDay_AddsToZeroBase()
    {
        // Overtime on a holiday: base zeroed by closure, extra still applies.
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithExtra("2026-06-15", "2026-06-15", TimeSpan.FromHours(4))
            .Build();
        var closures = new[] { CompanyClosureBuilder.SingleDay("2026-06-15") };

        var hours = CapacityCalculator.ForDate(resource, calendar, closures, new DateOnly(2026, 6, 15)); // Mon

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void ExtraTime_OnTopOfAbsence_SumThenClamp()
    {
        // base=8, absence=4, extra=2 -> 8-4+2 = 6
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(4))
            .WithExtra("2026-06-01", "2026-06-01", TimeSpan.FromHours(2))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.FromHours(6));
    }

    [Fact]
    public void Multiple_AbsencesAndExtras_SumThenClamp_OrderIndependent()
    {
        // base=8, A1=4, A2=4, E1=4 -> 8-4-4+4 = 4 (regardless of insertion order)
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(4))
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(4))
            .WithExtra("2026-06-01", "2026-06-01", TimeSpan.FromHours(4))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void Multiple_AbsencesExceedingBase_ClampsAtZero()
    {
        // base=8, A1=6, A2=6 -> 8-12 = -4 -> clamp 0
        var calendar = new BusinessCalendarBuilder().WithWindowOnDays(Weekdays, "09:00", "17:00").Build();
        var resource = new ResourceBuilder()
            .OnCalendar(calendar)
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(6))
            .WithPartialAbsence("2026-06-01", "2026-06-01", TimeSpan.FromHours(6))
            .Build();

        var hours = CapacityCalculator.ForDate(resource, calendar, [], new DateOnly(2026, 6, 1));

        hours.Should().Be(TimeSpan.Zero);
    }
}
