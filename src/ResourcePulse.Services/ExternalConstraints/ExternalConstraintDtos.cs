using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Services.ExternalConstraints;

public sealed class ExternalConstraintReadDto
{
    public Guid Id { get; init; }
    public Guid RootProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public ConstraintAuthority Authority { get; init; }
    public string? Notes { get; init; }

    // Coverage boundaries anchored to this date (ADR-0034 §8). Non-zero means
    // moving the date is a plan mutation: moveConstraint on the envelope, and
    // PUT refuses it with the count.
    public int AnchoredEdgeCount { get; set; }

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
    // A date change with anchored dependants is refused here with the count
    // (ADR-0034 §5) — use the plan command moveConstraint.
    public DateOnly Date { get; init; }
    public ConstraintAuthority Authority { get; init; }
    public string? Notes { get; init; }
}
