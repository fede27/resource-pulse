using FluentAssertions;
using ResourcePulse.Services.Capacity;

namespace ResourcePulse.Application.Tests;

// Pure run-length compression of the daily capacity series (P1 of
// api-roundtrip-consolidation.md): zero-hour days become gaps, equal
// consecutive hours collapse into one inclusive [From, To] segment.
public class CapacitySegmentsTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);

    private static DailyCapacityDto Day(int offset, double hours) =>
        new() { Date = Mon.AddDays(offset), Hours = TimeSpan.FromHours(hours) };

    [Fact]
    public void EmptySeries_YieldsNoSegments()
    {
        CapacitySegments.Compress([]).Should().BeEmpty();
    }

    [Fact]
    public void AllZeroDays_YieldNoSegments()
    {
        var days = Enumerable.Range(0, 7).Select(i => Day(i, 0)).ToList();

        CapacitySegments.Compress(days).Should().BeEmpty();
    }

    [Fact]
    public void WorkWeekWithWeekend_CompressesToRunsAroundTheGap()
    {
        // Mon–Fri 8h, Sat–Sun 0h, Mon–Wed 8h.
        var days = Enumerable.Range(0, 5).Select(i => Day(i, 8))
            .Concat([Day(5, 0), Day(6, 0)])
            .Concat(Enumerable.Range(7, 3).Select(i => Day(i, 8)))
            .ToList();

        var segments = CapacitySegments.Compress(days);

        segments.Should().HaveCount(2);
        segments[0].Should().BeEquivalentTo(
            new CapacitySegmentDto { From = Mon, To = Mon.AddDays(4), HoursPerDay = TimeSpan.FromHours(8) });
        segments[1].Should().BeEquivalentTo(
            new CapacitySegmentDto { From = Mon.AddDays(7), To = Mon.AddDays(9), HoursPerDay = TimeSpan.FromHours(8) });
    }

    [Fact]
    public void HoursChange_SplitsTheRun()
    {
        var days = new[] { Day(0, 8), Day(1, 8), Day(2, 4), Day(3, 4) };

        var segments = CapacitySegments.Compress(days);

        segments.Should().HaveCount(2);
        segments[0].To.Should().Be(Mon.AddDays(1));
        segments[0].HoursPerDay.Should().Be(TimeSpan.FromHours(8));
        segments[1].From.Should().Be(Mon.AddDays(2));
        segments[1].HoursPerDay.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void NonContiguousDates_SplitEvenWithEqualHours()
    {
        // A date hole (no entry at all) must break the run like a zero day does.
        var days = new[] { Day(0, 8), Day(1, 8), Day(3, 8) };

        var segments = CapacitySegments.Compress(days);

        segments.Should().HaveCount(2);
        segments[0].To.Should().Be(Mon.AddDays(1));
        segments[1].From.Should().Be(Mon.AddDays(3));
        segments[1].To.Should().Be(Mon.AddDays(3));
    }

    [Fact]
    public void SingleDay_IsADegenerateSegment()
    {
        var segments = CapacitySegments.Compress([Day(0, 6)]);

        segments.Should().ContainSingle();
        segments[0].From.Should().Be(Mon);
        segments[0].To.Should().Be(Mon);
        segments[0].HoursPerDay.Should().Be(TimeSpan.FromHours(6));
    }
}
