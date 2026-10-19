using TimeSloth.Domain.Projects;

namespace TimeSloth.Services.ExternalConstraints;

public sealed class ExternalConstraintReadDto
{
    public Guid Id { get; init; }
    public Guid RootProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public ConstraintAuthority Authority { get; init; }
    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed class CreateExternalConstraintDto
{
    public string Name { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public ConstraintAuthority Authority { get; init; } = ConstraintAuthority.Desiderata;
    public string? Notes { get; init; }
}

public sealed class UpdateExternalConstraintDto
{
    public string Name { get; init; } = string.Empty;
    // Freely editable (ADR-0035 §4): no boundary follows an imposed date, so
    // moving it moves nothing in the plan — it moves the delta.
    public DateOnly Date { get; init; }
    public ConstraintAuthority Authority { get; init; }
    public string? Notes { get; init; }
}
