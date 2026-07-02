using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Identity;

namespace ResourcePulse.Application.Tests;

// GET /api/me (gap #8 / ADR-0024): resolves the caller's identity + linked
// resource + role + a derived staffing-manager flag.
public class MeServiceTests
{
    private sealed class StubCurrentUser(bool authenticated, CurrentUser user) : ICurrentUserAccessor
    {
        public bool IsAuthenticated { get; } = authenticated;
        public CurrentUser User { get; } = user;
    }

    private static CurrentUser User(string sub, string name = "Claim Name", string email = "u@x", params (string, string)[] claims) =>
        new(sub, email, name, claims.ToDictionary(c => c.Item1, c => c.Item2));

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

        var svc = new MeService(new StubCurrentUser(true, User("sub-123", name: "Ignored Claim")), db);
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
        var svc = new MeService(new StubCurrentUser(true, User("unknown-sub", name: "Dev User")), db);

        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeTrue();
        me.ResourceId.Should().BeNull();
        me.RoleId.Should().BeNull();
        me.RoleName.Should().BeNull();
        me.Name.Should().Be("Dev User");
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("StaffingManager", true)]
    [InlineData("admin", true)]        // case-insensitive
    [InlineData("Viewer", false)]
    public async Task IsStaffingManager_DerivedFromRoleClaim(string roleClaim, bool expected)
    {
        var db = NewDb();
        var svc = new MeService(
            new StubCurrentUser(true, User("sub-x", claims: ("role", roleClaim))), db);

        var me = (await svc.GetAsync()).Value;

        me.IsStaffingManager.Should().Be(expected);
    }

    [Fact]
    public async Task NoRoleClaim_IsNotStaffingManager()
    {
        var db = NewDb();
        var svc = new MeService(new StubCurrentUser(true, User("sub-x")), db);

        (await svc.GetAsync()).Value.IsStaffingManager.Should().BeFalse();
    }

    [Fact]
    public async Task Unauthenticated_ReturnsNotAuthenticated()
    {
        var db = NewDb();
        var svc = new MeService(new StubCurrentUser(false, CurrentUser.Anonymous), db);

        var me = (await svc.GetAsync()).Value;

        me.IsAuthenticated.Should().BeFalse();
        me.ResourceId.Should().BeNull();
    }
}
