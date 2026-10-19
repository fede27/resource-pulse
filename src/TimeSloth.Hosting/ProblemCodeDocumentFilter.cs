using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using TimeSloth.Common.Results;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TimeSloth.Hosting;

// Publishes the ProblemCode catalogue (ADR-0037) as a component schema: a string
// enum of the wire slugs, named by x-enum-varnames. No operation references it —
// a code travels inside ProblemDetails.type, which OpenAPI cannot type — so it
// is added here explicitly, for the one consumer that matters: orval turns it
// into the client's ProblemCode, and the locales are keyed by that type.
internal sealed class ProblemCodeDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        var codes = Enum.GetValues<ProblemCode>();
        var values = new List<JsonNode>();
        var names = new JsonArray();
        foreach (var code in codes)
        {
            values.Add(JsonValue.Create(ProblemCodes.Slug(code)));
            names.Add(JsonValue.Create(code.ToString()));
        }

        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = $"A refusal the client can tell apart. Travels as ProblemDetails.type = \"{ProblemCodes.TypePrefix}{{code}}\", with its pieces in the `args` extension.",
            Enum = values,
        };
        schema.AddExtension("x-enum-varnames", new JsonNodeExtension(names));

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[nameof(ProblemCode)] = schema;
    }
}
