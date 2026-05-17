using ResourcePulse.Domain.Resources;
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

public sealed class ResourceReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid BusinessCalendarId { get; init; }
    public IReadOnlyList<WorkWindowDto> WorkWindows { get; init; } = Array.Empty<WorkWindowDto>();
    public IReadOnlyList<IndividualAdjustmentDto> Adjustments { get; init; } = Array.Empty<IndividualAdjustmentDto>();
}

public sealed class CreateResourceDto
{
    public string Name { get; init; } = string.Empty;
    public Guid? BusinessCalendarId { get; init; }
    public IReadOnlyList<WorkWindowDto>? Windows { get; init; }
    public IReadOnlyList<IndividualAdjustmentDto>? Adjustments { get; init; }
}

public sealed class UpdateResourceDto
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid BusinessCalendarId { get; init; }
}
