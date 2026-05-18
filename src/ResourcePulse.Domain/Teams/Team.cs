using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Teams;

public sealed class Team : Entity<Guid>, IAuditable
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Team() { }

    public static Team Create(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Team name must not be empty.");

        return new Team
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Team name must not be empty.");
        Name = trimmed;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
