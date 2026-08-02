using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ResourcePulse.Common.Tenancy;
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
using ResourcePulse.Persistence.Tenancy;

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
        // Control-plane configurations live in this same assembly but belong to a
        // different context; excluding them by namespace keeps the two models
        // (and their migration histories) from bleeding into each other.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ResourcePulseDbContext).Assembly,
            t => t.Namespace != typeof(ControlPlane.ControlPlaneDbContext).Namespace + ".Configurations");

        // Tenant isolation (ADR-0029). The context is pooled, so it cannot take
        // ITenantContext as a constructor dependency; it is read from the
        // application service provider instead. The registration is a singleton
        // that reads the ambient request at call time, so capturing it once while
        // the model is built is correct.
        var tenantContext = this.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?
            .ApplicationServiceProvider?
            .GetService<ITenantContext>();

        if (tenantContext is not null)
        {
            modelBuilder.ApplyTenantIsolation(tenantContext);
        }
        else
        {
            // Design time (migrations) and provider-agnostic unit tests: the
            // columns and indexes must still exist — only the runtime filter is
            // absent, and with no application host there is no request to scope.
            modelBuilder.ApplyTenantColumnsOnly();
        }
    }
}
