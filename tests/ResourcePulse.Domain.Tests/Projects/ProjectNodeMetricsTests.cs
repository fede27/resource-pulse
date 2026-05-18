using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeMetricsTests
{
    private static readonly DateOnly B1 = new(2026, 6, 1);
    private static readonly DateOnly B2 = new(2026, 8, 31);

    [Fact]
    public void DerivedStatus_NotStarted_WhenNoActuals()
    {
        var p = Project();

        ProjectNodeMetrics.DerivedStatus(p).Should().Be(DerivedExecutionStatus.NotStarted);
    }

    [Fact]
    public void DerivedStatus_InProgress_WhenStartedButNotEnded()
    {
        var p = Project();
        p.Start();

        ProjectNodeMetrics.DerivedStatus(p).Should().Be(DerivedExecutionStatus.InProgress);
    }

    [Fact]
    public void DerivedStatus_Completed_WhenBothActuals()
    {
        var p = Project();
        p.Start();
        p.Complete();

        ProjectNodeMetrics.DerivedStatus(p).Should().Be(DerivedExecutionStatus.Completed);
    }

    [Fact]
    public void ScheduleVarianceEnd_PositiveWhenLate()
    {
        var p = Project();
        p.Baseline(B1, B2);
        // Backfill an actual end 5 days after the baseline end.
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        p.BackfillActuals(pastStart, pastEnd);

        var variance = ProjectNodeMetrics.ScheduleVarianceEnd(p);

        variance.Should().Be(pastEnd.DayNumber - B2.DayNumber);
    }

    [Fact]
    public void Variances_NullWhenInputsMissing()
    {
        var p = Project();

        ProjectNodeMetrics.ScheduleVarianceStart(p).Should().BeNull();
        ProjectNodeMetrics.ScheduleVarianceEnd(p).Should().BeNull();
        ProjectNodeMetrics.ForecastVarianceEnd(p).Should().BeNull();
        ProjectNodeMetrics.IsLate(p).Should().BeNull();
    }

    [Fact]
    public void IsLate_TrueWhenPlannedEndAfterBaselineEnd()
    {
        var p = Project();
        p.Baseline(B1, B2);
        p.Replan(B1, B2.AddDays(10));

        ProjectNodeMetrics.IsLate(p).Should().BeTrue();
    }

    [Fact]
    public void IsLate_FalseWhenPlannedEndEqualOrBefore()
    {
        var p = Project();
        p.Baseline(B1, B2);
        p.Replan(B1, B2);

        ProjectNodeMetrics.IsLate(p).Should().BeFalse();
    }

    [Fact]
    public void ForecastVarianceEnd_SignedDays()
    {
        var p = Project();
        p.Baseline(B1, B2);
        p.Replan(B1, B2.AddDays(7));

        ProjectNodeMetrics.ForecastVarianceEnd(p).Should().Be(7);
    }
}
