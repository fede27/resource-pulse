namespace ResourcePulse.Services.Teams;

public sealed class TeamReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class CreateTeamDto
{
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTeamDto
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
