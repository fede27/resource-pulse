namespace ResourcePulse.Services.Tags;

public sealed class TagReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class CreateTagDto
{
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTagDto
{
    public string Name { get; init; } = string.Empty;
}
