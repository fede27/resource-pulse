using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ResourcePulse.Hosting;

// ASP.NET Core binds complex action parameters as [FromBody] by default,
// even on GET actions. That emits a requestBody in the OpenAPI document for
// GETs, which breaks tooling (orval misclassifies GET-with-body as a mutation).
// This filter removes the body from every GET operation.
internal sealed class StripBodyFromGetOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            operation.RequestBody = null;
    }
}
