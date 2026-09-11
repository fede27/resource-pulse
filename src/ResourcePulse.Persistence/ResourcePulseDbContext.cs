using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Access;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Domain.Teams;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence;

public class ResourcePulseDbContext(DbContextOptions<ResourcePulseDbContext> options) : DbContext(options)
{
    // The pooled context cannot take ITenantContext as a constructor dependency
    // (pooled instances are resolved from the root provider), so it is read off
    // the application service provider. The registration is a singleton that
    // resolves the ambient request — or the ambient TenantScope — at call time.
    private readonly ITenantContext? _tenantContext = options
        .FindExtension<CoreOptionsExtension>()?
        .ApplicationServiceProvider?
        .GetService<ITenantContext>();

    /// <summary>
    /// The tenant the global query filter compares against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It lives on the CONTEXT on purpose. A filter that reads the tenant off a
    /// captured <c>ITenantContext</c> is an expression EF considers evaluatable,
    /// so it <b>inlines the value as a SQL literal</b> and caches the compiled
    /// query — and every later execution, for every other tenant, reuses the
    /// first tenant's literal. A filter rooted at the DbContext is turned into an
    /// <c>__ef_filter__</c> parameter instead, and re-evaluated per execution.
    /// </para>
    /// <para>
    /// That is not a micro-optimization: with the literal baked in, the second
    /// tenant swept by the signal worker read the first tenant's rows — which is
    /// exactly the isolation the filter exists to provide. Nothing leaked, because
    /// RLS is the actual enforcement (ADR-0029) and refused the mismatched rows;
    /// what surfaced was a get-or-seed that saw "no configuration" and collided
    /// with the row that was already there.
    /// </para>
    /// </remarks>
    public Guid CurrentTenantId => _tenantContext?.TenantIdOrEmpty ?? Guid.Empty;

    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<CompanyClosure> CompanyClosures => Set<CompanyClosure>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProjectNode> ProjectNodes => Set<ProjectNode>();
    public DbSet<ExternalConstraint> ExternalConstraints => Set<ExternalConstraint>();
    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<Allocation> Allocations => Set<Allocation>();

    // Who may act in this tenant, and at what level (ADR-0030). Tenant data like
    // any other — the authorization store is ours, and RLS protects it too.
    public DbSet<Membership> Memberships => Set<Membership>();

    // Triage (ADR-0032). Persisted, not recomputed: "unseen" needs a
    // FirstDetectedAt, and the four verbs of the change feed are transitions of
    // these rows.
    public DbSet<PlanSignal> PlanSignals => Set<PlanSignal>();
    public DbSet<SignalSweepState> SignalSweepStates => Set<SignalSweepState>();
    public DbSet<SignalVisit> SignalVisits => Set<SignalVisit>();

    // Org-level configuration singletons (ADR-0020; SignalPolicy is the fifth,
    // ADR-0032 §12).
    public DbSet<LoadBandConfiguration> LoadBandConfigurations => Set<LoadBandConfiguration>();
    public DbSet<TimeFenceConfiguration> TimeFenceConfigurations => Set<TimeFenceConfiguration>();
    public DbSet<BucketingDefaults> BucketingDefaults => Set<BucketingDefaults>();
    public DbSet<CommitmentPolicyConfiguration> CommitmentPolicies => Set<CommitmentPolicyConfiguration>();
    public DbSet<SignalPolicy> SignalPolicies => Set<SignalPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Control-plane configurations live in this same assembly but belong to a
        // different context; excluding them by namespace keeps the two models
        // (and their migration histories) from bleeding into each other.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ResourcePulseDbContext).Assembly,
            t => t.Namespace != typeof(ControlPlane.ControlPlaneDbContext).Namespace + ".Configurations");

        // Tenant isolation (ADR-0029). The filter is rooted at THIS context, not at
        // the tenant accessor — see CurrentTenantId for why that distinction is
        // the difference between a parameter and a baked-in literal.
        if (_tenantContext is not null)
        {
            modelBuilder.ApplyTenantIsolation(this);
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
