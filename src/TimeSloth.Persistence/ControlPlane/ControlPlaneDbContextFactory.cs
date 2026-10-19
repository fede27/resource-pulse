using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeSloth.Persistence.ControlPlane;

// Used by EF Core tools (dotnet ef migrations add --context ControlPlaneDbContext)
// at design time. The real connection string comes from Aspire at runtime.
public sealed class ControlPlaneDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ControlPlaneDbContext>();
        optionsBuilder
            .UseNpgsql(
                "Host=localhost;Database=timesloth;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history", ControlPlaneDbContext.SchemaName))
            .UseSnakeCaseNamingConvention();
        return new ControlPlaneDbContext(optionsBuilder.Options);
    }
}
