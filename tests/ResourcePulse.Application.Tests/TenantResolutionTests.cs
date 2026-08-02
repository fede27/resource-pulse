using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Tenancy;
using ResourcePulse.Persistence.ControlPlane;
using ResourcePulse.Services.Tenancy;

namespace ResourcePulse.Application.Tests;

// org -> TenantId resolution against the control-plane registry (ADR-0029).
//
// The point of these tests is the FAIL-CLOSE branches. A resolver that silently
// falls back to "the first tenant", "a default tenant" or "no filter" turns a
// mapping gap into a cross-tenant data leak, so each way of failing is asserted
// to produce a non-resolved outcome with an empty tenant id.
public class TenantResolutionTests
{
    private const string OrgId = "298392038479283";

    private static ControlPlaneDbContext NewControlPlane() =>
        new(new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase($"control-plane-{Guid.NewGuid()}")
            .Options);

    private static ClaimsPrincipal PrincipalWith(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestScheme"));

    private static async Task<ControlPlaneDbContext> WithTenantAsync(
        string organizationId,
        TenantStatus status = TenantStatus.Active)
    {
        var db = NewControlPlane();
        var tenant = Tenant.Create(organizationId, "Acme");

        switch (status)
        {
            case TenantStatus.Suspended: tenant.Suspend(); break;
            case TenantStatus.Archived: tenant.Archive(); break;
        }

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task MappedActiveOrganization_ResolvesToOurTenantId()
    {
        await using var db = await WithTenantAsync(OrgId);
        var expected = await db.Tenants.Select(t => t.Id).SingleAsync();

        var result = await new TenantResolver(db).ResolveByOrganizationAsync(OrgId);

        result.IsResolved.Should().BeTrue();
        result.Outcome.Should().Be(TenantResolutionOutcome.Resolved);
        // The organization id is the lookup KEY, never the tenant id itself.
        result.TenantId.Should().Be(expected);
        result.TenantId.ToString().Should().NotBe(OrgId);
    }

    [Fact]
    public async Task ZitadelOrganizationClaim_IsReadFromThePrincipal()
    {
        await using var db = await WithTenantAsync(OrgId);
        var expected = await db.Tenants.Select(t => t.Id).SingleAsync();

        var result = await new TenantResolver(db).ResolveAsync(
            PrincipalWith((TenantResolver.ZitadelOrgIdClaim, OrgId)));

        result.TenantId.Should().Be(expected);
    }

    [Fact]
    public async Task GenericOrganizationClaim_IsAlsoAccepted()
    {
        await using var db = await WithTenantAsync(OrgId);
        var expected = await db.Tenants.Select(t => t.Id).SingleAsync();

        var result = await new TenantResolver(db).ResolveAsync(
            PrincipalWith((TenantResolver.GenericOrgIdClaim, OrgId)));

        result.TenantId.Should().Be(expected);
    }

    // ── Fail-close branches ──────────────────────────────────────────────────

    [Fact]
    public async Task UnmappedOrganization_FailsClosed()
    {
        await using var db = await WithTenantAsync(OrgId);

        var result = await new TenantResolver(db).ResolveByOrganizationAsync("999-unknown-org");

        result.IsResolved.Should().BeFalse();
        result.Outcome.Should().Be(TenantResolutionOutcome.OrganizationNotMapped);
        result.TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task UnmappedOrganization_DoesNotFallBackToTheOnlyTenant()
    {
        // The regression this guards: with exactly one tenant in the registry it
        // is tempting — and catastrophic — to treat it as the default.
        await using var db = await WithTenantAsync(OrgId);
        (await db.Tenants.CountAsync()).Should().Be(1);

        var result = await new TenantResolver(db).ResolveByOrganizationAsync("some-other-org");

        result.TenantId.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Archived)]
    public async Task NonActiveTenant_FailsClosed(TenantStatus status)
    {
        await using var db = await WithTenantAsync(OrgId, status);

        var result = await new TenantResolver(db).ResolveByOrganizationAsync(OrgId);

        result.IsResolved.Should().BeFalse();
        result.Outcome.Should().Be(TenantResolutionOutcome.TenantNotActive);
        result.TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task PrincipalWithoutOrganizationClaim_FailsClosed()
    {
        await using var db = await WithTenantAsync(OrgId);

        var result = await new TenantResolver(db).ResolveAsync(
            PrincipalWith((ClaimTypes.NameIdentifier, "user-1")));

        result.IsResolved.Should().BeFalse();
        result.Outcome.Should().Be(TenantResolutionOutcome.MissingOrganizationClaim);
        result.TenantId.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankOrganizationId_FailsClosed(string? organizationId)
    {
        await using var db = await WithTenantAsync(OrgId);

        var result = await new TenantResolver(db).ResolveByOrganizationAsync(organizationId);

        result.Outcome.Should().Be(TenantResolutionOutcome.MissingOrganizationClaim);
        result.TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task OrganizationClaim_IsTrimmed()
    {
        await using var db = await WithTenantAsync(OrgId);
        var expected = await db.Tenants.Select(t => t.Id).SingleAsync();

        var result = await new TenantResolver(db).ResolveByOrganizationAsync($"  {OrgId}  ");

        result.TenantId.Should().Be(expected);
    }

    [Fact]
    public async Task ReLinkingAnOrganization_KeepsTheTenantIdStable()
    {
        // Why the indirection exists at all: re-federating a customer must not
        // rewrite a single row of planning data.
        await using var db = await WithTenantAsync(OrgId);
        var tenant = await db.Tenants.SingleAsync();
        var originalId = tenant.Id;

        tenant.RelinkIdentityOrganization("new-org-id");
        await db.SaveChangesAsync();

        var result = await new TenantResolver(db).ResolveByOrganizationAsync("new-org-id");

        result.TenantId.Should().Be(originalId);
        (await new TenantResolver(db).ResolveByOrganizationAsync(OrgId)).IsResolved.Should().BeFalse();
    }

    // ── TenantScope: the only sanctioned non-request tenant source ────────────

    [Fact]
    public void TenantScope_RestoresThePreviousValueOnDispose()
    {
        TenantScope.CurrentTenantId.Should().BeNull();
        var outer = Guid.NewGuid();

        using (TenantScope.For(outer))
        {
            TenantScope.CurrentTenantId.Should().Be(outer);

            var inner = Guid.NewGuid();
            using (TenantScope.For(inner))
                TenantScope.CurrentTenantId.Should().Be(inner);

            TenantScope.CurrentTenantId.Should().Be(outer);
        }

        TenantScope.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public void TenantScope_RejectsTheEmptyTenant()
    {
        // Guid.Empty is the "no tenant" sentinel the query filter relies on to
        // match nothing; allowing it as a scope would invert that meaning.
        var act = () => TenantScope.For(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }
}
