using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;

namespace ResourcePulse.Application.Tests;

// The global query filter (ADR-0029) must be re-evaluated on EVERY execution.
//
// It was not. Reading the tenant off the captured ITenantContext produced an
// expression EF considers evaluatable, so it computed the value ONCE, wrote it
// into the SQL as a literal, and cached the compiled query — after which every
// execution, for every other tenant, reused the FIRST tenant's id:
//
//     WHERE t.tenant_id = 'e9eeeb12-…'      ← literal, not a parameter
//
// It surfaced as the signal worker failing on the second tenant it swept: the
// config get-or-seed read the wrong tenant's rows, saw nothing for its own, and
// collided with the row that was already there. Nothing leaked — RLS is the real
// enforcement and refused the mismatched rows — but the filter was inert, and on
// a read path RLS is the only thing that would have stood between two tenants.
public class TenantQueryFilterTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // EF caches the built model per (context type, design-time). Every other test
    // in this assembly builds ResourcePulseDbContext WITHOUT an application
    // service provider — i.e. columns only, no filter — and whichever model is
    // built first wins for the whole run. Without a distinct cache key these tests
    // would pass alone and fail in the suite (or, worse, impose the filter on
    // everyone else and break them instead).
    private sealed class IsolatedModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.GetType(), designTime, Marker: nameof(TenantQueryFilterTests));
    }

    private static ResourcePulseDbContext NewDb(string name, IServiceProvider services) =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase(name)
            .UseApplicationServiceProvider(services)
            .ReplaceService<IModelCacheKeyFactory, IsolatedModelCacheKeyFactory>()
            .Options);

    // Outside a request the tenant comes from TenantScope — the worker's path.
    private static IServiceProvider AmbientTenancy() =>
        new ServiceCollection()
            .AddSingleton<ITenantContext, AmbientTenantContext>()
            .BuildServiceProvider();

    private static void SeedRole(ResourcePulseDbContext db, Guid tenantId, string name)
    {
        var role = Role.Create(name);
        var entry = db.Roles.Add(role);
        // The stamp interceptor is not wired in these provider-agnostic tests;
        // the shadow property is set directly so the filter has something to
        // match against.
        entry.Property("TenantId").CurrentValue = tenantId;
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public void TheFilterFollowsTheSCOPE_AcrossSuccessiveQueriesOnTheSameContext()
    {
        var name = $"filter-{Guid.NewGuid()}";
        var services = AmbientTenancy();

        using (var seed = NewDb(name, services))
        {
            SeedRole(seed, TenantA, "Backend");
            SeedRole(seed, TenantB, "Frontend");
        }

        using var db = NewDb(name, services);

        // The FIRST execution is the one whose value used to get baked into the
        // cached plan.
        List<string> first;
        using (var _ = TenantScope.For(TenantA))
            first = db.Roles.Select(r => r.Name).ToList();

        List<string> second;
        using (var _ = TenantScope.For(TenantB))
            second = db.Roles.Select(r => r.Name).ToList();

        first.Should().Equal("Backend");
        // The assertion that would have failed before the fix: the filter must be
        // re-evaluated, not reused from the first execution's compiled plan.
        second.Should().Equal("Frontend");
    }

    [Fact]
    public void TheFilterFollowsTheSCOPE_AcrossSuccessiveContexts()
    {
        // The worker's actual shape: one DI scope — and one pooled context — per
        // tenant, in a loop. The model, and therefore the filter, is built once.
        var name = $"filter-{Guid.NewGuid()}";
        var services = AmbientTenancy();

        using (var seed = NewDb(name, services))
        {
            SeedRole(seed, TenantA, "Backend");
            SeedRole(seed, TenantB, "Frontend");
        }

        using (var _ = TenantScope.For(TenantA))
        using (var db = NewDb(name, services))
            db.Roles.Select(r => r.Name).ToList().Should().Equal("Backend");

        using (var _ = TenantScope.For(TenantB))
        using (var db = NewDb(name, services))
            db.Roles.Select(r => r.Name).ToList().Should().Equal("Frontend");
    }

    [Fact]
    public void WithNoScopeOpen_NothingIsVisible()
    {
        // Fail-close: an unresolved tenant degrades to Guid.Empty, and no row
        // carries it. A unit of work that forgot to open a scope reads an empty
        // database rather than somebody else's.
        var name = $"filter-{Guid.NewGuid()}";
        var services = AmbientTenancy();

        using (var seed = NewDb(name, services))
            SeedRole(seed, TenantA, "Backend");

        using var db = NewDb(name, services);
        db.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ANestedScopeRestoresTheOuterTenantOnDispose()
    {
        var name = $"filter-{Guid.NewGuid()}";
        var services = AmbientTenancy();

        using (var seed = NewDb(name, services))
        {
            SeedRole(seed, TenantA, "Backend");
            SeedRole(seed, TenantB, "Frontend");
        }

        using var db = NewDb(name, services);

        using var outer = TenantScope.For(TenantA);
        using (var inner = TenantScope.For(TenantB))
            db.Roles.Select(r => r.Name).ToList().Should().Equal("Frontend");

        db.Roles.Select(r => r.Name).ToList().Should().Equal("Backend");
    }
}
