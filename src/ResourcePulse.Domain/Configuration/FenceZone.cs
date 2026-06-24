namespace ResourcePulse.Domain.Configuration;

// The three zones of the rolling time fence (ADR-0020). The trichotomy is a
// CONSTANT: only the boundaries between zones are configured. The zone→behaviour
// mapping (frozen makes disruptive ops evident/attritate via consequence/dryRun)
// is a SYSTEM RULE, not config, and lands with the disruptive operations (§5).
public enum FenceZone
{
    Frozen = 1,
    Slushy = 2,
    Liquid = 3
}
