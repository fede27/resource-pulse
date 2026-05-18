namespace ResourcePulse.Domain.Projects;

// Pure derivations from a ProjectNode's date fields. Not persisted; computed on demand.
public static class ProjectNodeMetrics
{
    // Days between ActualStart and BaselineStart. Positive = late start.
    public static int? ScheduleVarianceStart(ProjectNode node) =>
        DiffDays(node.ActualStart, node.BaselineStart);

    // Days between ActualEnd and BaselineEnd. Positive = late finish.
    public static int? ScheduleVarianceEnd(ProjectNode node) =>
        DiffDays(node.ActualEnd, node.BaselineEnd);

    // Days between PlannedEnd and BaselineEnd. Positive = forecast slipping.
    public static int? ForecastVarianceEnd(ProjectNode node) =>
        DiffDays(node.PlannedEnd, node.BaselineEnd);

    public static bool? IsLate(ProjectNode node) =>
        node.PlannedEnd is { } p && node.BaselineEnd is { } b ? p > b : null;

    public static DerivedExecutionStatus DerivedStatus(ProjectNode node) =>
        (node.ActualStart, node.ActualEnd) switch
        {
            (null, _)        => DerivedExecutionStatus.NotStarted,
            (not null, null) => DerivedExecutionStatus.InProgress,
            (not null, _)    => DerivedExecutionStatus.Completed
        };

    private static int? DiffDays(DateOnly? later, DateOnly? earlier) =>
        later is { } l && earlier is { } e ? l.DayNumber - e.DayNumber : null;
}
