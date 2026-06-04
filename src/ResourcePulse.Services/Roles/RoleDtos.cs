namespace ResourcePulse.Services.Roles;

public sealed class RoleReadDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class CreateRoleDto
{
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateRoleDto
{
    public string Name { get; init; } = string.Empty;
}
