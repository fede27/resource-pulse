using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Access;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Application.Tests;

// Administering who may act in the tenant (ADR-0030). The two guards under test
// are both statements the aggregate cannot make on its own: one is about the SET
// of memberships, the other about the CALLER.
public class MembershipAdministrationTests
{
    private const string CallerSub = "owner-sub";

    private sealed class StubCurrentUser(string sub) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public CurrentUser User { get; } = new(sub, "owner@x.com", "Owner", new Dictionary<string, string>());
        public string? AuthenticationScheme => "TestScheme";
    }

    private static ResourcePulseDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"memberships-{Guid.NewGuid()}")
            .Options);

    private static MembershipService ServiceFor(ResourcePulseDbContext db, string callerSub = CallerSub) =>
        new(db, new StubCurrentUser(callerSub));

    private static async Task<(ResourcePulseDbContext Db, Membership Caller)> WithOwnerAsync(
        params (string Email, AppRole Role, string? Sub)[] others)
    {
        var db = NewDb();

        var caller = Membership.Invite("owner@x.com", AppRole.Owner);
        caller.ClaimBy(CallerSub, "Owner");
        db.Memberships.Add(caller);

        foreach (var (email, role, sub) in others)
        {
            var m = Membership.Invite(email, role);
            if (sub is not null) m.ClaimBy(sub);
            db.Memberships.Add(m);
        }

        await db.SaveChangesAsync();
        return (db, caller);
    }

    [Fact]
    public async Task Invite_CreatesAPendingGrant()
    {
        var (db, _) = await WithOwnerAsync();

        var result = await ServiceFor(db).InviteAsync(
            new InviteMembershipDto { Email = "anna@x.com", Role = AppRole.Planner });

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPending.Should().BeTrue();
        result.Value.Role.Should().Be(AppRole.Planner);
    }

    // Locking yourself out is never what you meant, and another owner can do it.
    [Fact]
    public async Task ChangingYourOwnRole_IsRefused()
    {
        var (db, caller) = await WithOwnerAsync(("second@x.com", AppRole.Owner, "sub-2"));

        var result = await ServiceFor(db).ChangeRoleAsync(
            caller.Id, new UpdateMembershipRoleDto { Role = AppRole.Viewer });

        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }

    [Fact]
    public async Task RevokingYourOwnMembership_IsRefused()
    {
        var (db, caller) = await WithOwnerAsync(("second@x.com", AppRole.Owner, "sub-2"));

        var result = await ServiceFor(db).RevokeAsync(caller.Id);

        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }

    // Demoting the last owner leaves a tenant nobody can ever administer again —
    // including to undo the demotion.
    [Fact]
    public async Task DemotingTheLastOwner_IsRefused()
    {
        var (db, _) = await WithOwnerAsync(("other@x.com", AppRole.Planner, "sub-2"));
        var lastOwner = await db.Memberships.SingleAsync(m => m.Role == AppRole.Owner);

        // Act as somebody else, so the self-guard is not what refuses this.
        var result = await ServiceFor(db, "sub-2").ChangeRoleAsync(
            lastOwner.Id, new UpdateMembershipRoleDto { Role = AppRole.Planner });

        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        result.Error!.Message.Should().Contain("last owner");
    }

    [Fact]
    public async Task RevokingTheLastOwner_IsRefused()
    {
        var (db, _) = await WithOwnerAsync(("other@x.com", AppRole.Planner, "sub-2"));
        var lastOwner = await db.Memberships.SingleAsync(m => m.Role == AppRole.Owner);

        var result = await ServiceFor(db, "sub-2").RevokeAsync(lastOwner.Id);

        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }

    [Fact]
    public async Task DemotingAnOwner_IsAllowedWhileAnotherRemains()
    {
        var (db, _) = await WithOwnerAsync(("second@x.com", AppRole.Owner, "sub-2"));
        var second = await db.Memberships.SingleAsync(m => m.UserSub == "sub-2");

        var result = await ServiceFor(db).ChangeRoleAsync(
            second.Id, new UpdateMembershipRoleDto { Role = AppRole.Viewer });

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(AppRole.Viewer);
    }

    // A pending owner invite counts towards the owner tally: it is a grant somebody
    // made, and re-inviting is always available if it goes unclaimed.
    [Fact]
    public async Task APendingOwnerInvite_CountsAsAnOwner()
    {
        var (db, _) = await WithOwnerAsync(("pending@x.com", AppRole.Owner, null));
        var claimedOwner = await db.Memberships.SingleAsync(m => m.UserSub == CallerSub);

        var result = await ServiceFor(db, "other-sub").ChangeRoleAsync(
            claimedOwner.Id, new UpdateMembershipRoleDto { Role = AppRole.Viewer });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FlagsTheCallersOwnRow()
    {
        var (db, _) = await WithOwnerAsync(("anna@x.com", AppRole.Viewer, "sub-2"));

        var rows = (await ServiceFor(db).GetAllAsync()).Value;

        rows.Should().HaveCount(2);
        rows.Single(r => r.Email == "owner@x.com").IsSelf.Should().BeTrue();
        rows.Single(r => r.Email == "anna@x.com").IsSelf.Should().BeFalse();
    }
}
