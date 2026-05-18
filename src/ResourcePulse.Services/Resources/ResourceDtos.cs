using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Resources;

public sealed class IndividualAdjustmentDto
{
    public Guid Id { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public AdjustmentType Type { get; init; }
    public TimeSpan? Hours { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class ResourceSkillDto
{
    public Guid SkillId { get; init; }
    public SkillLevel Level { get; init; }
}

public sealed class ResourceTagDto
{
    public Guid TagId { get; init; }
}

public sealed class ResourceReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid BusinessCalendarId { get; init; }
    public Guid? TeamId { get; init; }
    public IReadOnlyList<WorkWindowDto> WorkWindows { get; init; } = Array.Empty<WorkWindowDto>();
    public IReadOnlyList<IndividualAdjustmentDto> Adjustments { get; init; } = Array.Empty<IndividualAdjustmentDto>();
    public IReadOnlyList<ResourceSkillDto> Skills { get; init; } = Array.Empty<ResourceSkillDto>();
    public IReadOnlyList<ResourceTagDto> Tags { get; init; } = Array.Empty<ResourceTagDto>();
}

public sealed class CreateResourceDto
{
    public string Name { get; init; } = string.Empty;
    public Guid? BusinessCalendarId { get; init; }
    public Guid? TeamId { get; init; }
    public IReadOnlyList<WorkWindowDto>? Windows { get; init; }
    public IReadOnlyList<IndividualAdjustmentDto>? Adjustments { get; init; }
    public IReadOnlyList<ResourceSkillDto>? Skills { get; init; }
    public IReadOnlyList<ResourceTagDto>? Tags { get; init; }
}

public sealed class UpdateResourceDto
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid BusinessCalendarId { get; init; }
}

public sealed class AssignTeamDto
{
    // Null clears the assignment.
    public Guid? TeamId { get; init; }
}

public sealed class AddOrUpdateResourceSkillDto
{
    public Guid SkillId { get; init; }
    public SkillLevel Level { get; init; }
}

public sealed class AddResourceTagDto
{
    public Guid TagId { get; init; }
}
