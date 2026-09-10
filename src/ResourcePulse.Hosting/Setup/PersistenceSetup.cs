using Microsoft.EntityFrameworkCore.Infrastructure;
using ResourcePulse.Domain;
using ResourcePulse.Persistence;
using ResourcePulse.Persistence.ControlPlane;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Hosting.Setup;

/// <summary>
/// The database: interceptors, the pooled domain context and the control-plane
/// registry, opened on the non-superuser role that makes RLS real.
/// </summary>
/// <remarks>
/// The credentials themselves are derived by <see cref="AppDbConnection"/> in
/// Persistence, shared with the signal worker: which role constrains us is one
/// question, and it must have one answer. The resolved pair is registered rather
/// than kept in a local because the dev bootstrap needs the owner connection long
/// after the container is built — and threading it through <c>Program.cs</c> would
/// put "which credentials?" back in the one file that must not answer it casually.
/// </remarks>
public static class PersistenceSetup
{
    public static void AddResourcePulsePersistence(this WebApplicationBuilder builder)
    {
        // AuditInterceptor is singleton (safe — reads IHttpContextAccessor.HttpContext at call time).
        // Wired into the DbContextPool's shared options via IDbContextOptionsConfiguration<T>,
        // which is the EF Core-supported way to add interceptors when pooling is active.
        builder.Services.AddSingleton<AuditInterceptor>();
        // Tenant isolation (ADR-0029): the stamp interceptor sets tenant_id on insert,
        // the session interceptor publishes it to Postgres for the RLS policies.
        builder.Services.AddSingleton<TenantStampInterceptor>();
        builder.Services.AddSingleton<TenantSessionInterceptor>();
        builder.Services.AddSingleton<IDbContextOptionsConfiguration<ResourcePulseDbContext>,
            ResourcePulseDbContextOptionsConfiguration>();
        if (builder.Environment.IsDevelopment())
        {
            // Adds EnableSensitiveDataLogging + EnableDetailedErrors. Composes with the
            // base configuration above. Lives in Hosting because the env decision belongs here.
            builder.Services.AddSingleton<IDbContextOptionsConfiguration<ResourcePulseDbContext>,
                DevDiagnosticsDbContextOptionsConfiguration>();
        }
        builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

        var connections = AppDbConnection.Resolve(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSingleton(connections);

        builder.AddNpgsqlDbContext<ResourcePulseDbContext>(AppDbConnection.ResourceName, settings =>
        {
            if (connections.Application is not null) settings.ConnectionString = connections.Application;
        });

        // The control-plane registry: read-only to the API, its own schema and its own
        // migrations history. Not tenant-scoped — it is what establishes the tenant.
        builder.AddNpgsqlDbContext<ControlPlaneDbContext>(
            AppDbConnection.ResourceName,
            settings =>
            {
                if (connections.Application is not null) settings.ConnectionString = connections.Application;
            },
            ControlPlaneDbContext.ConfigureOptions);
    }
}
