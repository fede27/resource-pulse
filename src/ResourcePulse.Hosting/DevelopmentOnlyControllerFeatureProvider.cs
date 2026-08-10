using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ResourcePulse.Http.Auth;

namespace ResourcePulse.Hosting;

/// <summary>
/// Drops <see cref="DevelopmentOnlyAttribute"/> controllers outside Development.
/// </summary>
/// <remarks>
/// Removing the controller from the discovered feature — rather than refusing the
/// request inside it — means the route does not exist at all: nothing to call,
/// nothing published in the OpenAPI document, and no code path where a future edit
/// could accidentally weaken the check.
/// </remarks>
public sealed class DevelopmentOnlyControllerFeatureProvider(bool isDevelopment)
    : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (isDevelopment) return;

        var devOnly = feature.Controllers
            .Where(c => c.IsDefined(typeof(DevelopmentOnlyAttribute), inherit: false))
            .ToList();

        foreach (var controller in devOnly)
            feature.Controllers.Remove(controller);
    }
}
