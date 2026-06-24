namespace ResourcePulse.Domain.Configuration;

// Unit of a rolling horizon Duration (ADR-0020). First-class: the unit is part
// of the configured boundary so "2 weeks" (an agile sprint as the frozen zone)
// is expressible without weeks/days being cabled.
public enum DurationUnit
{
    Days = 1,
    Weeks = 2,
    Months = 3
}
