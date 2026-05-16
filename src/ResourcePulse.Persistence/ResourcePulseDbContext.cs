using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain;

namespace ResourcePulse.Persistence;

public class ResourcePulseDbContext(DbContextOptions<ResourcePulseDbContext> options) : DbContext(options)
{
    public DbSet<Ping> Pings => Set<Ping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourcePulseDbContext).Assembly);
    }
}
