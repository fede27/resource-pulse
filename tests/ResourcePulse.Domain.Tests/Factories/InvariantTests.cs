namespace ResourcePulse.Domain.Tests.Factories;

public class InvariantTests
{
    [Fact]
    public void WorkWindow_Create_StartNotBeforeEnd_Throws()
    {
        var act = () => WorkWindow.Create(
            DayOfWeek.Monday,
            new TimeOnly(17, 0),
            new TimeOnly(9, 0),
            new DateOnly(2026, 1, 1),
            null);

        act.Should().Throw<DomainException>()
            .WithMessage("*start time must be earlier than end time*");
    }

    [Fact]
    public void WorkWindow_Create_ValidToBeforeOrEqualValidFrom_Throws()
    {
        var act = () => WorkWindow.Create(
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1)); // equal to ValidFrom — invalid (half-open)

        act.Should().Throw<DomainException>()
            .WithMessage("*ValidTo must be later than ValidFrom*");
    }

    [Fact]
    public void IndividualAdjustment_Create_ExtraTimeWithoutHours_Throws()
    {
        var act = () => IndividualAdjustment.Create(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1),
            AdjustmentType.ExtraTime,
            hours: null,
            reason: "Overtime",
            notes: null);

        act.Should().Throw<DomainException>()
            .WithMessage("*ExtraTime adjustment requires Hours*");
    }

    [Fact]
    public void IndividualAdjustment_Create_ExtraTimeNonPositiveHours_Throws()
    {
        var act = () => IndividualAdjustment.Create(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1),
            AdjustmentType.ExtraTime,
            hours: TimeSpan.Zero,
            reason: "Bad",
            notes: null);

        act.Should().Throw<DomainException>()
            .WithMessage("*Hours must be positive*");
    }

    [Fact]
    public void IndividualAdjustment_Create_DateFromAfterDateTo_Throws()
    {
        var act = () => IndividualAdjustment.Create(
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 1),
            AdjustmentType.Absence,
            hours: null,
            reason: "Vacation",
            notes: null);

        act.Should().Throw<DomainException>()
            .WithMessage("*DateFrom must be on or before DateTo*");
    }

    [Fact]
    public void BusinessCalendar_AddWorkWindow_OverlappingValidity_AndTime_SameDay_Throws()
    {
        var calendar = BusinessCalendar.Create("Test", false);
        var first = WorkWindow.Create(
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            new DateOnly(2026, 1, 1),
            null);
        calendar.AddWorkWindow(first);

        // Same day, overlapping validity (both open-ended) AND overlapping time — double-count.
        var second = WorkWindow.Create(
            DayOfWeek.Monday,
            new TimeOnly(12, 0),
            new TimeOnly(18, 0),
            new DateOnly(2026, 6, 1),
            null);

        var act = () => calendar.AddWorkWindow(second);

        act.Should().Throw<DomainException>()
            .WithMessage("*overlaps an existing window's time of day*");
    }

    [Fact]
    public void BusinessCalendar_AddWorkWindow_LunchBreak_Allowed()
    {
        // Two same-day windows that don't overlap in time (a typical lunch break) must be allowed.
        var calendar = BusinessCalendar.Create("Test", false);
        calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0), new DateOnly(2026, 1, 1), null));

        var act = () => calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0), new DateOnly(2026, 1, 1), null));

        act.Should().NotThrow();
    }

    [Fact]
    public void BusinessCalendar_AddWorkWindow_DifferentDay_AlwaysAllowed()
    {
        var calendar = BusinessCalendar.Create("Test", false);
        calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), new DateOnly(2026, 1, 1), null));

        var act = () => calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0), new DateOnly(2026, 1, 1), null));

        act.Should().NotThrow();
    }

    [Fact]
    public void BusinessCalendar_AddWorkWindow_SameDayNonOverlappingValidity_Allowed()
    {
        // Mon 9-17 [2024-01-01, 2026-01-01) then Mon 10-14 [2026-01-01, null) — non-overlapping validity
        var calendar = BusinessCalendar.Create("Test", false);
        calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0),
            new DateOnly(2024, 1, 1), new DateOnly(2026, 1, 1)));

        var act = () => calendar.AddWorkWindow(WorkWindow.Create(
            DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0),
            new DateOnly(2026, 1, 1), null));

        act.Should().NotThrow();
    }

    [Fact]
    public void Resource_Create_EmptyName_Throws()
    {
        var act = () => Resource.Create("   ", Guid.NewGuid());

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void Resource_Create_EmptyCalendarId_Throws()
    {
        var act = () => Resource.Create("Alice", Guid.Empty);

        act.Should().Throw<DomainException>()
            .WithMessage("*must reference a business calendar*");
    }

    [Fact]
    public void CompanyClosure_Create_FromAfterTo_Throws()
    {
        var act = () => CompanyClosure.Create(
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 1),
            "Bad");

        act.Should().Throw<DomainException>()
            .WithMessage("*DateFrom must be on or before DateTo*");
    }
}
