using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ResourcePulse.Hosting;

// Adds the `x-enum-varnames` OpenAPI vendor extension to every enum schema so
// downstream codegen tools (orval, NSwag, openapi-generator, ...) can emit
// meaningful member names rather than synthetic placeholders like NUMBER_0,
// NUMBER_1, ... The wire format stays integer — this only annotates the schema
// with the original CLR enum names.
internal sealed class EnumVarnamesSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;
        // ISchemaFilter exposes the read-only interface; only the concrete
        // OpenApiSchema implements IOpenApiExtensible, which is needed to add
        // vendor extensions.
        if (schema is not OpenApiSchema writable) return;

        var names = Enum.GetNames(context.Type);
        var array = new JsonArray();
        foreach (var name in names)
            array.Add(JsonValue.Create(name));

        writable.AddExtension("x-enum-varnames", new JsonNodeExtension(array));
    }
}
