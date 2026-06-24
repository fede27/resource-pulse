using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Domain.Configuration;

// Org-level singleton (ADR-0020). The EXTRACTION of the hard-commit threshold
// (invariant I6 / "0b"): the set of CommitmentLevel values that count as
// "hard-committed" and therefore permit allocations with Status = Hard. This is
// NOT a new rule — I6 is unchanged; only where it reads the threshold moves here.
// PlanCommandService (Hard gate) and ProjectNodeService (cascade-demotion
// threshold) both consult this aggregate; no second source of truth.
//
// Persistence detail: the set is stored as a CSV of level names in a single
// column (_hardCommitLevels). The aggregate is the source of truth; the CSV is an
// internal representation parsed/formatted here.
public sealed class CommitmentPolicyConfiguration : Entity<Guid>, IAuditable
{
    public static readonly Guid SingletonId = new("a1b1c1d1-0000-0000-0000-000000000004");

    private string _hardCommitLevels = string.Empty;

    public IReadOnlyCollection<CommitmentLevel> HardCommitLevels => Parse(_hardCommitLevels);

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    private CommitmentPolicyConfiguration() { }

    // Opinionated default {Committed, Critical} — the value previously cabled.
    public static CommitmentPolicyConfiguration CreateDefault() =>
        Create(SingletonId, [CommitmentLevel.Committed, CommitmentLevel.Critical]);

    public static CommitmentPolicyConfiguration Create(Guid id, IReadOnlyCollection<CommitmentLevel> hardCommitLevels)
    {
        var config = new CommitmentPolicyConfiguration { Id = id };
        config.SetHardCommitLevels(hardCommitLevels);
        return config;
    }

    public void Replace(IReadOnlyCollection<CommitmentLevel> hardCommitLevels) =>
        SetHardCommitLevels(hardCommitLevels);

    private void SetHardCommitLevels(IReadOnlyCollection<CommitmentLevel> hardCommitLevels)
    {
        if (hardCommitLevels is null || hardCommitLevels.Count == 0)
            throw new DomainException("CommitmentPolicy must list at least one hard-commit level.");

        foreach (var level in hardCommitLevels)
            if (!Enum.IsDefined(level))
                throw new DomainException($"Invalid commitment level '{level}'.");

        var distinct = hardCommitLevels.Distinct().OrderBy(l => (int)l).ToList();
        _hardCommitLevels = string.Join(',', distinct.Select(l => l.ToString()));
    }

    // The single decision point for "does this commitment level permit Hard?".
    public bool IsHardCommitted(CommitmentLevel? level) =>
        level is { } l && HardCommitLevels.Contains(l);

    private static IReadOnlyCollection<CommitmentLevel> Parse(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Enum.Parse<CommitmentLevel>)
            .ToList();
}
