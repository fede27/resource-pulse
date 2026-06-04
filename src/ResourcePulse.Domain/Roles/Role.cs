using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Roles;

// Centralized catalogue of job roles ("Sviluppatore", "Designer", "Team Lead",
// ...). Mirrors the Skill/Tag pattern: shared across resources, unique by
// case-insensitive name (citext at the DB level), display case preserved.
public sealed class Role : Entity<Guid>, IAuditable
{
    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Role() { }

    public static Role Create(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Role name must not be empty.");

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = trimmed
        };
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Role name must not be empty.");
        Name = trimmed;
    }
}
