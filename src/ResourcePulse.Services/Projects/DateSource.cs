namespace ResourcePulse.Services.Projects;

// Controls which date set GetProjectsActiveInRange filters on.
// Effective = use Actual where available, fall back to Planned. Locks in the
// extensibility point flagged in the Phase 3 spec for the future load-matching code.
public enum DateSource
{
    Planned = 0,
    Baseline = 1,
    Effective = 2
}
