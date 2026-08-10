using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain.Access;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Access;
using ResourcePulse.Services.Identity;

namespace ResourcePulse.Application.Tests;

// GET /api/me (gap #8 / ADR-0024): the caller's identity + linked resource + job
// role, plus the application role held in this tenant (ADR-0030).
public class MeServiceTests
{
    private sealed class StubCurrentUser(bool authenticated, CurrentUser user, string? scheme = "FakeAuth")
        : ICurrentUserAccessor
    {
        public bool IsAuthenticated { get; } = authenticated;
        public CurrentUser User { get; } = user;
        public string? AuthenticationScheme { get; } = authenticated ? scheme : null;
    }

    private sealed class StubAccess(AppRole? role) : ICurrentAccess
    {
        public AppRole? Role { get; } = role;
        public bool IsMember => Role is not null;
        public bool Has(AppRole required) => Role is { } r && r >= required;
    }

    private static CurrentUser User(string sub, string name = "Claim Name", string email = "u@x") =>
        new(sub, email, name, new Dictionary<string, string>());

    private static ResourcePulseDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"me-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task LinkedResourceWithRole_ResolvesResourceRoleAndName()
    {
        var db = NewDb();
        var role = Role.Create("Team Lead");
        var res = Resource.Create("Tizio", Guid.NewGuid());
        res.AssignToRole(role.Id);
        res.LinkToUser("sub-123");
        db.Roles.Add(role);
        db.Resources.Add(res);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new MeService(
            new StubCurrentUser(true, User("sub-123", name: "Ignored Claim")),
            new StubAccess(AppRole.Planner),
            db);
        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeTrue();
        me.Sub.Should().Be("sub-123");
        me.ResourceId.Should().Be(res.Id);
        me.RoleId.Should().Be(role.Id);
        me.RoleName.Should().Be("Team Lead");
        me.Name.Should().Be("Tizio"); // resource name wins over claim name
    }

    [Fact]
    public async Task NoLinkedResource_FallsBackToClaimName_NullResource()
    {
        var db = NewDb();
        var svc = new MeService(
            new StubCurrentUser(true, User("unknown-sub", name: "Dev User")),
            new StubAccess(AppRole.Viewer),
            db);

        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeTrue();
        me.ResourceId.Should().BeNull();
        me.RoleId.Should().BeNull();
        me.RoleName.Should().BeNull();
        me.Name.Should().Be("Dev User");
    }

    [Theory]
    [InlineData(AppRole.Viewer)]
    [InlineData(AppRole.Planner)]
    [InlineData(AppRole.Owner)]
    public async Task AccessRole_ComesFromTheMembershipStore(AppRole role)
    {
        var db = NewDb();
        var svc = new MeService(new StubCurrentUser(true, User("sub-x")), new StubAccess(role), db);

        var me = (await svc.GetAsync()).Value;

        me.IsMember.Should().BeTrue();
        me.AccessRole.Should().Be(role);
    }

    // The endpoint is deliberately exempt from the membership requirement: it must
    // answer for a non-member so the client can render "you have no access" rather
    // than a blank screen (ADR-0030).
    [Fact]
    public async Task AuthenticatedNonMember_IsAuthenticatedButNotAMember()
    {
        var db = NewDb();
        var svc = new MeService(new StubCurrentUser(true, User("stranger")), new StubAccess(null), db);

        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeTrue();
        me.IsMember.Should().BeFalse();
        me.AccessRole.Should().BeNull();
    }

    [Fact]
    public async Task Unauthenticated_ReturnsNotAuthenticated()
    {
        var db = NewDb();
        var svc = new MeService(
            new StubCurrentUser(false, CurrentUser.Anonymous),
            new StubAccess(null),
            db);

        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeFalse();
        me.ResourceId.Should().BeNull();
        me.IsMember.Should().BeFalse();
    }
}
