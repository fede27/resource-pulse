using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Access;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Application.Tests;

// Principal -> membership resolution (ADR-0030).
//
// Like tenant resolution, the branches that matter are the ones that must NOT
// invent access: an authenticated stranger, a claimed invite belonging to someone
// else, a request with no tenant. Each has to come back a non-member, because the
// alternative is an authorization the operator never granted.
public class AccessResolutionTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class StubCurrentUser(bool authenticated, CurrentUser user) : ICurrentUserAccessor
    {
        public bool IsAuthenticated { get; } = authenticated;
        public CurrentUser User { get; } = user;
        public string? AuthenticationScheme => IsAuthenticated ? "TestScheme" : null;
    }

    private sealed class StubTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId ?? throw new InvalidOperationException("No tenant.");
        public Guid TenantIdOrEmpty => tenantId ?? Guid.Empty;
        public bool IsResolved => tenantId is not null;
    }

    private static CurrentUser User(string sub, string email = "", string name = "Tizio") =>
        new(sub, email, name, new Dictionary<string, string>());

    private static ResourcePulseDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"access-{Guid.NewGuid()}")
            .Options);

    private static AccessResolver ResolverFor(
        ResourcePulseDbContext db,
        CurrentUser user,
        bool authenticated = true,
        Guid? tenantId = null) =>
        new(new StubCurrentUser(authenticated, user),
            new StubTenantContext(tenantId ?? TenantId),
            db);

    private static async Task<ResourcePulseDbContext> WithMembershipAsync(
        string email,
        AppRole role,
        string? claimedBySub = null)
    {
        var db = NewDb();
        var membership = Membership.Invite(email, role);
        if (claimedBySub is not null) membership.ClaimBy(claimedBySub);
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task ClaimedMembership_ResolvesByIdentitySubject()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Planner, "sub-1");

        var result = await ResolverFor(db, User("sub-1")).ResolveAsync();

        result.IsMember.Should().BeTrue();
        result.Role.Should().Be(AppRole.Planner);
    }

    // The grant necessarily predates the subject: an IdP mints it at first login.
    // Without this branch, the very first real sign-in would be a 403 with no way
    // out.
    [Fact]
    public async Task PendingInvite_IsClaimedByEmailOnFirstSignIn()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner);

        var result = await ResolverFor(db, User("sub-new", email: "anna@x.com", name: "Anna Blu"))
            .ResolveAsync();

        result.IsMember.Should().BeTrue();
        result.Role.Should().Be(AppRole.Owner);

        db.ChangeTracker.Clear();
        var stored = await db.Memberships.SingleAsync();
        stored.UserSub.Should().Be("sub-new");
        stored.DisplayName.Should().Be("Anna Blu");
        stored.IsPending.Should().BeFalse();
    }

    // An invite already bound to someone else is not a spare key.
    [Fact]
    public async Task AlreadyClaimedInvite_IsNotReclaimedByAnotherSubject()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner, "sub-1");

        var result = await ResolverFor(db, User("sub-2", email: "anna@x.com")).ResolveAsync();

        result.IsMember.Should().BeFalse();
        result.Outcome.Should().Be(AccessOutcome.NotAMember);
    }

    [Fact]
    public async Task AuthenticatedStranger_IsNotAMember()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner, "sub-1");

        var result = await ResolverFor(db, User("stranger", email: "stranger@x.com")).ResolveAsync();

        result.IsMember.Should().BeFalse();
        result.Outcome.Should().Be(AccessOutcome.NotAMember);
        result.Role.Should().BeNull();
    }

    [Fact]
    public async Task NoEmailClaim_CannotClaimAnInvite()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner);

        var result = await ResolverFor(db, User("sub-new")).ResolveAsync();

        result.IsMember.Should().BeFalse();
    }

    [Fact]
    public async Task Unauthenticated_ResolvesToNothing()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner, "sub-1");

        var result = await ResolverFor(db, CurrentUser.Anonymous, authenticated: false).ResolveAsync();

        result.Outcome.Should().Be(AccessOutcome.NotAuthenticated);
    }

    // The membership table is tenant-scoped and RLS-protected: resolving access
    // before the tenant is in the session would read nothing and turn every caller
    // into a non-member — a fail-close that reads like a configuration bug. The
    // resolver refuses to run at all rather than produce that.
    [Fact]
    public async Task NoResolvedTenant_ResolvesToNothing()
    {
        await using var db = await WithMembershipAsync("anna@x.com", AppRole.Owner, "sub-1");

        var resolver = new AccessResolver(
            new StubCurrentUser(true, User("sub-1")),
            new StubTenantContext(null),
            db);

        (await resolver.ResolveAsync()).Outcome.Should().Be(AccessOutcome.NotAuthenticated);
    }
}
