namespace ResourcePulse.Services.Access;

/// <summary>
/// Resolves the current principal to a membership in the current tenant.
/// </summary>
public interface IAccessResolver
{
    Task<AccessResolution> ResolveAsync(CancellationToken ct = default);
}
