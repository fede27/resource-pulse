namespace ResourcePulse.Services.Skills;

public sealed class SkillReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
}

public sealed class CreateSkillDto
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
}

public sealed class UpdateSkillDto
{
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
}
