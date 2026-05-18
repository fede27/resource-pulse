using System.Globalization;
using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Tags;

public sealed class Tag : Entity<Guid>, IAuditable
{
    // Stored already normalized (trimmed + lower-invariant). Uniqueness is enforced
    // against the normalized form via a plain unique index — no citext needed.
    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private Tag() { }

    public static Tag Create(string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
            throw new DomainException("Tag name must not be empty.");

        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = normalized
        };
    }

    public void Rename(string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
            throw new DomainException("Tag name must not be empty.");
        Name = normalized;
    }

    // InvariantCulture avoids locale-specific lowercase quirks (e.g. Turkish dotless i).
    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLower(CultureInfo.InvariantCulture);
}
