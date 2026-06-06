using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Services.Projects;

// ── Read DTOs ───────────────────────────────────────────────────────────────

public sealed class ProjectSkillRequirementDto
{
    public Guid SkillId { get; init; }
    public SkillLevel MinLevel { get; init; }
}

public sealed class ProjectNodeTagDto
{
    public Guid TagId { get; init; }
}

public sealed class ProjectNodeReadDto
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public ProjectNodeType NodeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string Path { get; init; } = string.Empty;
    public int Depth { get; init; }

    public ProjectType? Type { get; init; }
    public CommitmentLevel? CommitmentLevel { get; init; }
    public Guid? LeadResourceId { get; init; }
    public ProjectStatus? Status { get; init; }

    public DateOnly? BaselineStart { get; init; }
    public DateOnly? BaselineEnd { get; init; }
    public DateTimeOffset? BaselinedAt { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedEnd { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualEnd { get; init; }

    public PlanningMode? PlanningMode { get; init; }
    public TimeSpan? EstimatedWork { get; init; }

    // Derived — populated on detail reads (GetByIdAsync, GetSubtreeAsync). Listing
    // endpoints that go through DevExtreme projection leave these null; clients can
    // compute them locally with the same formulas.
    public int? ScheduleVarianceStart { get; set; }
    public int? ScheduleVarianceEnd { get; set; }
    public int? ForecastVarianceEnd { get; set; }
    public bool? IsLate { get; set; }
    public DerivedExecutionStatus? DerivedStatus { get; set; }

    public IReadOnlyList<ProjectSkillRequirementDto> SkillRequirements { get; init; } = Array.Empty<ProjectSkillRequirementDto>();
    public IReadOnlyList<ProjectNodeTagDto> Tags { get; init; } = Array.Empty<ProjectNodeTagDto>();
}

// ── Create / Update DTOs ────────────────────────────────────────────────────

public sealed class CreateProjectNodeDto
{
    public Guid? ParentId { get; init; }
    public ProjectNodeType NodeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }

    // Required when NodeType == Project; ignored otherwise.
    public ProjectType? Type { get; init; }
    public CommitmentLevel? CommitmentLevel { get; init; }
    public Guid? LeadResourceId { get; init; }
}

public sealed class UpdateProjectNodeDto
{
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
}

public sealed class UpdateProjectDto
{
    public ProjectType Type { get; init; }
    public CommitmentLevel CommitmentLevel { get; init; }
    public Guid? LeadResourceId { get; init; }

    // Conferma esplicita richiesta dall'invariante I6 (ADR-0015 §4): un
    // downgrade da {Committed, Critical} a {Exploratory, Planned} con
    // allocazioni Hard nella subtree del progetto le demota a Tentative.
    // L'operazione non è silenziosa — richiede ConfirmDemoteHardAllocations =
    // true; altrimenti restituisce Conflict con il conteggio.
    public bool ConfirmDemoteHardAllocations { get; init; }
}

// ── Operation DTOs ──────────────────────────────────────────────────────────

public sealed class ReparentDto
{
    public Guid NewParentId { get; init; }
}

public sealed class BaselineDto
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }
}

public sealed class RebaselineDto
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReplanDto
{
    public DateOnly? Start { get; init; }
    public DateOnly? End { get; init; }
}

public sealed class BackfillActualsDto
{
    public DateOnly? Start { get; init; }
    public DateOnly? End { get; init; }
}

public sealed class ReasonDto
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class AddOrUpdateProjectSkillRequirementDto
{
    public Guid SkillId { get; init; }
    public SkillLevel MinLevel { get; init; }
}

public sealed class AddProjectNodeTagDto
{
    public Guid TagId { get; init; }
}

public sealed class SetPlanningModeDto
{
    public PlanningMode Mode { get; init; }
    public TimeSpan? EstimatedWork { get; init; }
}

public sealed class UpdateEstimatedWorkDto
{
    public TimeSpan EstimatedWork { get; init; }
}
