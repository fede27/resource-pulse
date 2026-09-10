namespace ResourcePulse.Hosting.Setup;

/// <summary>
/// The OpenAPI document, which exists for one consumer: the orval code generator
/// that produces the SPA's client from a checked-in snapshot of it.
/// </summary>
/// <remarks>
/// Development-only, and the environment check lives in here rather than around
/// the call: "should this exist in production?" is a question about Swagger, not
/// about the startup sequence. Every option below is load-bearing for codegen —
/// none is decoration.
/// </remarks>
public static class SwaggerSetup
{
    public static void AddResourcePulseSwagger(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            // Required for orval/openapi codegen: default operationIds collide
            // across controllers (every CRUD controller has GetAll/GetById/...).
            c.CustomOperationIds(api =>
                $"{api.ActionDescriptor.RouteValues["controller"]}_{api.ActionDescriptor.RouteValues["action"]}");

            // Plan command union (ADR-0018): render the System.Text.Json polymorphic
            // PlanCommand as `oneOf` + discriminator ("kind") so orval emits a tagged
            // union on the client. Inheritance schemas use allOf.
            c.UseOneOfForPolymorphism();
            c.UseAllOfForInheritance();

            // Drop request bodies from GET operations (DataSourceLoadOptionsBase
            // would otherwise be emitted as a body, breaking GET semantics).
            c.OperationFilter<StripBodyFromGetOperationFilter>();

            // Annotate enum schemas with x-enum-varnames so codegen tools (orval,
            // NSwag, ...) emit meaningful member names instead of NUMBER_0,
            // NUMBER_1, ... The wire format stays integer.
            c.SchemaFilter<EnumVarnamesSchemaFilter>();
        });
    }

    public static void UseResourcePulseSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        app.UseSwagger();
        app.UseSwaggerUI();
    }
}
