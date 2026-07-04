using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProjectNode> ProjectNodes => Set<ProjectNode>();
    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<Allocation> Allocations => Set<Allocation>();

    // Org-level configuration singletons (ADR-0020).
    public DbSet<LoadBandConfiguration> LoadBandConfigurations => Set<LoadBandConfiguration>();
    public DbSet<TimeFenceConfiguration> TimeFenceConfigurations => Set<TimeFenceConfiguration>();
    public DbSet<BucketingDefaults> BucketingDefaults => Set<BucketingDefaults>();
    public DbSet<CommitmentPolicyConfiguration> CommitmentPolicies => Set<CommitmentPolicyConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResourcePulseDbContext).Assembly);
    }
}
