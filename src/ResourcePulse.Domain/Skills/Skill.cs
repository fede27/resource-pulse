using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Skills;

public sealed class Skill : Entity<Guid>, IAuditable
{
    public string Name { get; private set; } = string.Empty;
    public string? Category { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Skill() { }

    public static Skill Create(string name, string? category = null)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            throw new DomainException("Skill name must not be empty.");

        return new Skill
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Category = NormalizeOptional(category)
        };
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Skill name must not be empty.");
        Name = trimmed;
    }

    public void ChangeCategory(string? category) => Category = NormalizeOptional(category);

    private static string? NormalizeOptional(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
