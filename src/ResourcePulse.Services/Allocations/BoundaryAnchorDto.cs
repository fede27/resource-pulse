using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Allocations;

// Read shape of one block boundary (ADR-0034 §8): what it is tied to, and to
// whom. ReferentName is resolved on the read projection (ADR-0024 pattern) and
// stays null on the structural PlanBlockChange, which is computable in dryRun
// without touching the DB (ADR-0018 §2).
public sealed class BoundaryAnchorDto
{
    public AnchorKind Kind { get; init; }
    public Guid? NodeId { get; init; }
    public Guid? ConstraintId { get; init; }
    public string? ReferentName { get; init; }

    public static BoundaryAnchorDto From(BoundaryAnchor a, string? referentName = null) => new()
    {
        Kind = a.Kind,
        NodeId = a.NodeId,
        ConstraintId = a.ConstraintId,
        ReferentName = referentName
    };
}
