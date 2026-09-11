using ResourcePulse.Common.Domain;

namespace ResourcePulse.Domain.Allocations;

// Owned value object: the semantics of ONE block boundary (ADR-0034 §1).
// Immutable — an Allocation replaces the instance, never mutates it. Two per
// block (StartAnchor / EndAnchor), stored inline as flat columns.
//
// The referent is typed by kind: NodeStart/NodeEnd carry a NodeId, External a
// ConstraintId, Pinned and ResourceAvailability carry nothing (the latter's
// referent is the block's own resource). The shape is a CHECK on the table as
// well, so a dangling or mis-typed referent cannot be stored.
//
// The anchor does NOT carry a date. The boundary's date lives on the block
// (PeriodStart / PeriodEnd) and, while anchored, EQUALS the referent's date —
// invariant I9, maintained by construction (snap on anchoring, re-snap on
// propagation, pin on any other move). No offset: "coincides", not "follows at
// a distance".
public sealed class BoundaryAnchor : IEquatable<BoundaryAnchor>
{
    public AnchorKind Kind { get; private set; }
    public Guid? NodeId { get; private set; }
    public Guid? ConstraintId { get; private set; }

    private BoundaryAnchor() { }

    public static BoundaryAnchor Pinned() => new() { Kind = AnchorKind.Pinned };

    public static BoundaryAnchor ToNodeStart(Guid nodeId) => ToNode(AnchorKind.NodeStart, nodeId);

    public static BoundaryAnchor ToNodeEnd(Guid nodeId) => ToNode(AnchorKind.NodeEnd, nodeId);

    public static BoundaryAnchor ToResourceAvailability() => new() { Kind = AnchorKind.ResourceAvailability };

    public static BoundaryAnchor ToExternal(Guid constraintId)
    {
        if (constraintId == Guid.Empty)
            throw new DomainException("An External anchor must reference a constraint.");
        return new BoundaryAnchor { Kind = AnchorKind.External, ConstraintId = constraintId };
    }

    // Builds from the flat shape (kind + optional referents), validating that the
    // referent matches the kind. The single entry point for the service layer.
    public static BoundaryAnchor Of(AnchorKind kind, Guid? nodeId, Guid? constraintId)
    {
        if (!Enum.IsDefined(kind))
            throw new DomainException($"Invalid anchor kind '{kind}'.");

        var anchor = kind switch
        {
            AnchorKind.Pinned => Pinned(),
            AnchorKind.NodeStart => ToNodeStart(nodeId ?? Guid.Empty),
            AnchorKind.NodeEnd => ToNodeEnd(nodeId ?? Guid.Empty),
            AnchorKind.ResourceAvailability => ToResourceAvailability(),
            AnchorKind.External => ToExternal(constraintId ?? Guid.Empty),
            _ => throw new DomainException($"Invalid anchor kind '{kind}'.")
        };

        // A referent the kind does not use is a malformed anchor, not noise.
        if (!anchor.RequiresNode && nodeId is not null)
            throw new DomainException($"A {kind} anchor does not take a node referent.");
        if (!anchor.RequiresConstraint && constraintId is not null)
            throw new DomainException($"A {kind} anchor does not take a constraint referent.");

        return anchor;
    }

    // A fresh instance with the same value. Needed wherever an anchor is handed
    // from one block to another (split): the persistence model owns one instance
    // per block.
    public BoundaryAnchor Copy() => new() { Kind = Kind, NodeId = NodeId, ConstraintId = ConstraintId };

    public bool IsAnchored => Kind != AnchorKind.Pinned;
    public bool RequiresNode => Kind is AnchorKind.NodeStart or AnchorKind.NodeEnd;
    public bool RequiresConstraint => Kind == AnchorKind.External;

    private static BoundaryAnchor ToNode(AnchorKind kind, Guid nodeId)
    {
        if (nodeId == Guid.Empty)
            throw new DomainException($"A {kind} anchor must reference a project node.");
        return new BoundaryAnchor { Kind = kind, NodeId = nodeId };
    }

    public bool Equals(BoundaryAnchor? other) =>
        other is not null && Kind == other.Kind && NodeId == other.NodeId && ConstraintId == other.ConstraintId;

    public override bool Equals(object? obj) => Equals(obj as BoundaryAnchor);

    public override int GetHashCode() => HashCode.Combine(Kind, NodeId, ConstraintId);

    public override string ToString() => Kind switch
    {
        AnchorKind.NodeStart or AnchorKind.NodeEnd => $"{Kind}({NodeId})",
        AnchorKind.External => $"{Kind}({ConstraintId})",
        _ => Kind.ToString()
    };
}
