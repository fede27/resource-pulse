namespace TimeSloth.Domain.Projects;

/// <summary>
/// What kind of work a root project is (ADR-0038). Root-only, mandatory.
/// <para>
/// The order IS the layering order: an overload erodes Transform first, then Grow,
/// then Run. Do not reorder or renumber, and do not add a value without an ADR.
/// Who pays is not this axis — that is <see cref="ProjectNode.Client"/>.
/// </para>
/// </summary>
public enum ProjectType
{
    /// <summary>Keeping existing commitments alive: maintenance, support, operations.</summary>
    Run = 1,
    /// <summary>Expanding the current business: new customer projects, new features on established lines.</summary>
    Grow = 2,
    /// <summary>Changing what the company is: new products and offerings, R&amp;D, new capabilities.</summary>
    Transform = 3
}
