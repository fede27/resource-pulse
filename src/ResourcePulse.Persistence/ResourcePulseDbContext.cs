using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Domain.Teams;

namespace ResourcePulse.Persistence;

public class ResourcePulseDbContext(DbContextOptions<ResourcePulseDbContext> options) : DbContext(options)
{
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<CompanyClosure> CompanyClosures => Set<CompanyClosure>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProjectNode> ProjectNodes => Set<ProjectNode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourcePulseDbContext).Assembly);
    }
}
