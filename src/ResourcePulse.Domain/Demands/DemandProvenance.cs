namespace ResourcePulse.Domain.Demands;

// How a demand came to exist (revision §5). Metadata for explainability, not a
// subtype: both provenances are the same aggregate with the same lifecycle.
//
//   Declared — received from outside already decomposed by role in hours.
//   Inferred — materialized from the coverage gesture ("you put Luca as PM ⇒
//              there is a PM demand"). The role is seeded from the person's role
//              as a suggestion and is correctable afterward (Demand.ChangeRole).
public enum DemandProvenance
{
    Declared = 0,
    Inferred
}
