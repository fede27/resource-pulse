namespace ResourcePulse.Http.Auth;

/// <summary>
/// Marks a controller that must not exist outside Development.
/// </summary>
/// <remarks>
/// Enforced by removing the type from the MVC application parts at startup (see
/// <c>DevelopmentOnlyControllerFeatureProvider</c> in the host), so the endpoint is
/// not merely refused — it is not routed, not discoverable and absent from the
/// OpenAPI document. The check is on the running environment rather than a
/// compile-time symbol: a build flag would tie the guarantee to how the assembly
/// was produced instead of to how the process is running.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DevelopmentOnlyAttribute : Attribute;
