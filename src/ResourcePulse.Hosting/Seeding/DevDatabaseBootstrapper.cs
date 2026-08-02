using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Tenancy;
using ResourcePulse.Persistence;
using ResourcePulse.Persistence.ControlPlane;

namespace ResourcePulse.Hosting.Seeding;

/// <summary>
/// Development-only database bring-up on the <b>owner</b> connection (ADR-0029).
/// </summary>
/// <remarks>
/// The API's own connection is a NOSUPERUSER role with no DDL rights and with
/// row-level security in force, so schema work cannot go through the registered
/// (application) contexts. These short-lived contexts are built by hand against
/// the owner credentials for exactly that reason, and for nothing else.
/// </remarks>
public static class DevDatabaseBootstrapper
{
    /// <summary>
    /// The organization id FakeAuth issues. It is a real row in the real
    /// registry — the fake path resolves its tenant through the same lookup the
    /// Zitadel path uses, so the fail-close logic is never bypassed in dev.
    /// </summary>
    public const string FakeOrganizationId = "dev-fake-org";

    public static async Task MigrateAsync(string? ownerConnectionString, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(ownerConnectionString))
        {
            logger.LogWarning("No owner connection string available; skipping migrations.");
            return;
        }

        await using (var control = CreateControlPlaneContext(ownerConnectionString))
            await control.Database.MigrateAsync();

        await using (var domain = CreateDomainContext(ownerConnectionString))
            await domain.Database.MigrateAsync();

        logger.LogInformation("Database migrations applied (control plane + domain).");
    }

    /// <summary>
    /// Ensures the development tenant exists and returns its id. Idempotent: the
    /// organization id is the natural key, so a re-run reuses the same tenant.
    /// </summary>
    public static async Task<Guid> EnsureDevTenantAsync(string? ownerConnectionString, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerConnectionString);

        await using var control = CreateControlPlaneContext(ownerConnectionString);

        var existing = await control.Tenants
            .FirstOrDefaultAsync(t => t.IdentityOrganizationId == FakeOrganizationId);

        if (existing is not null) return existing.Id;

        var tenant = Tenant.Create(FakeOrganizationId, "Development");
        control.Tenants.Add(tenant);
        await control.SaveChangesAsync();

        logger.LogInformation("Registered development tenant {TenantId}.", tenant.Id);
        return tenant.Id;
    }

    private static ControlPlaneDbContext CreateControlPlaneContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", ControlPlaneDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options);

    private static ResourcePulseDbContext CreateDomainContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}
