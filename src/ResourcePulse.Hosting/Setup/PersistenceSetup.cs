using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using ResourcePulse.Domain;
using ResourcePulse.Persistence;
using ResourcePulse.Persistence.ControlPlane;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Hosting.Setup;

/// <summary>
/// The two connection strings this process uses, resolved once at startup.
/// </summary>
/// <remarks>
/// Registered rather than returned because the dev bootstrap needs the owner
/// connection long after the container is built, and threading it through a
/// local would put the "which credentials?" question back in <c>Program.cs</c> —
/// which is the one place it must not be answered casually.
/// </remarks>
public sealed record DatabaseConnections(string? Owner, string? Application);

/// <summary>
/// The database: interceptors, the pooled domain context, the control-plane
/// registry, and the two-credential split that makes row-level security real.
/// </summary>
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

        var connections = ResolveConnections(builder);
        builder.Services.AddSingleton(connections);

        builder.AddNpgsqlDbContext<ResourcePulseDbContext>("resourcepulse-db", settings =>
        {
            if (connections.Application is not null) settings.ConnectionString = connections.Application;
        });

        // The control-plane registry: read-only to the API, its own schema and its own
        // migrations history. Not tenant-scoped — it is what establishes the tenant.
        builder.AddNpgsqlDbContext<ControlPlaneDbContext>(
            "resourcepulse-db",
            settings =>
            {
                if (connections.Application is not null) settings.ConnectionString = connections.Application;
            },
            options => options
                .UseSnakeCaseNamingConvention()
                .UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history", ControlPlaneDbContext.SchemaName)));
    }

    // ── Two connections, deliberately ────────────────────────────────────────
    // Aspire hands us the OWNER credentials. The API must not use them at runtime:
    // a Postgres superuser bypasses row-level security unconditionally, so every
    // tenant policy would be inert (ADR-0029). The request path therefore connects
    // as a dedicated NOSUPERUSER NOBYPASSRLS role created by the container init
    // script, while DDL — migrations and dev seeding — stays on the owner.
    //
    // The signal worker derives the same pair from the same configuration keys with
    // its own copy of this logic; review finding 12 is to move this method into
    // Persistence so both processes read one implementation.
    private static DatabaseConnections ResolveConnections(WebApplicationBuilder builder)
    {
        var ownerConnectionString = builder.Configuration.GetConnectionString("resourcepulse-db");
        var appDbRole = builder.Configuration["Tenancy:AppDbRole"];
        var appDbPassword = builder.Configuration["Tenancy:AppDbPassword"];

        if (string.IsNullOrWhiteSpace(ownerConnectionString) || string.IsNullOrWhiteSpace(appDbRole))
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Tenancy:AppDbRole is required outside Development: without a non-superuser role the " +
                    "row-level-security policies do not constrain the application.");

            return new DatabaseConnections(ownerConnectionString, null);
        }

        var owner = new NpgsqlConnectionStringBuilder(ownerConnectionString);

        var applicationConnectionString = new NpgsqlConnectionStringBuilder(ownerConnectionString)
        {
            // Pin the database BEFORE swapping the user. Aspire's connection string
            // carries no Database=, and Npgsql then defaults it to the username — so
            // changing the username would silently retarget a database named after
            // the application role, which does not exist.
            Database = string.IsNullOrEmpty(owner.Database) ? owner.Username : owner.Database,
            Username = appDbRole,
            Password = appDbPassword
        }.ConnectionString;

        return new DatabaseConnections(ownerConnectionString, applicationConnectionString);
    }
}
