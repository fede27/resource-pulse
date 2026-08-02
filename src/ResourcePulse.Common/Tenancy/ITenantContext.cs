namespace ResourcePulse.Common.Tenancy;

/// <summary>
/// The tenant the current operation runs within. Populated exclusively from a
/// validated token's organization claim, resolved through the control-plane
/// registry (ADR-0029). Never from a header, route value or subdomain.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The resolved tenant. Throws <see cref="InvalidOperationException"/> when
    /// nothing has been resolved — callers that mutate data must never guess.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// The resolved tenant, or <see cref="Guid.Empty"/> when unresolved. Used by
    /// the EF global query filter, where "unresolved" must degrade to *matching
    /// nothing* rather than throwing: no row carries an empty tenant, so an
    /// unresolved context sees an empty database (fail-close).
    /// </summary>
    Guid TenantIdOrEmpty { get; }

    bool IsResolved { get; }
}
