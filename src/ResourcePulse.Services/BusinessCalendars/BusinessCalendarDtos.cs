using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.BusinessCalendars;

public sealed class BusinessCalendarReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public IReadOnlyList<WorkWindowDto> WorkWindows { get; init; } = Array.Empty<WorkWindowDto>();
}

public sealed class CreateBusinessCalendarDto
{
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public IReadOnlyList<WorkWindowDto>? Windows { get; init; }
}

public sealed class UpdateBusinessCalendarDto
{
    public string Name { get; init; } = string.Empty;
}
