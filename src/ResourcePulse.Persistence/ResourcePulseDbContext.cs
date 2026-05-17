using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Persistence;

public class ResourcePulseDbContext(DbContextOptions<ResourcePulseDbContext> options) : DbContext(options)
{
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<CompanyClosure> CompanyClosures => Set<CompanyClosure>();
    public DbSet<Resource> Resources => Set<Resource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourcePulseDbContext).Assembly);
    }
}
