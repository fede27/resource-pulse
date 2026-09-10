using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Tenancy;

namespace ResourcePulse.Persistence.ControlPlane;

/// <summary>
/// The control plane: our own tenant registry (ADR-0029). Deliberately a
/// separate <see cref="DbContext"/> from <see cref="ResourcePulseDbContext"/>.
/// <para>
/// It is <b>not</b> tenant-scoped and carries <b>no</b> row-level security: it is
/// read to <i>establish</i> the tenant context, so filtering it by tenant would be
/// circular. It lives in its own <c>control_plane</c> schema with its own
/// migrations history so the two schemas never collide.
/// </para>
/// </summary>
public class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public const string SchemaName = "control_plane";

    /// <summary>
    /// The options both hosts must apply when they open this context: its own
    /// migrations history table, inside its own schema. If the two hosts spelled
    /// this differently, each would believe the other's migrations had not run.
    /// </summary>
    public static void ConfigureOptions(DbContextOptionsBuilder options) =>
        options
            .UseSnakeCaseNamingConvention()
            .UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history", SchemaName));

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ControlPlaneDbContext).Assembly,
            t => t.Namespace == typeof(ControlPlaneDbContext).Namespace + ".Configurations");
    }
}
