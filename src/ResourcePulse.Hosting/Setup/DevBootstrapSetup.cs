using ResourcePulse.Common.Tenancy;
using ResourcePulse.Hosting.Seeding;
using ResourcePulse.Persistence;

namespace ResourcePulse.Hosting.Setup;

/// <summary>
/// Migrations and seeding on Development startup. In production migrations are
/// applied out-of-band by CI/CD and nothing here runs.
/// </summary>
public static class DevBootstrapSetup
{
    public static async Task RunResourcePulseDevBootstrapAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        var connections = app.Services.GetRequiredService<DatabaseConnections>();

        // DDL runs on the OWNER connection: the application role deliberately has no
        // rights to create tables or alter policies. Seeding below stays on the
        // application connection, so it exercises the isolation rather than
        // sidestepping it.
        await DevDatabaseBootstrapper.MigrateAsync(connections.Owner, app.Logger);

        // Everything below is tenant data, so it needs a tenant. Development gets a
        // deterministic one, registered in the control plane against the fake
        // organization FakeAuth issues — so even the fake path resolves its tenant
        // through the real registry rather than bypassing it.
        var devTenantId = await DevDatabaseBootstrapper.EnsureDevTenantAsync(
            connections.Owner, app.Logger);

        using (TenantScope.For(devTenantId))
        {
            await DevSeeder.SeedAsync(app.Services, app.Logger);
            await BulkDevSeeder.SeedAsync(app.Services, app.Logger);
        }
    }
}
