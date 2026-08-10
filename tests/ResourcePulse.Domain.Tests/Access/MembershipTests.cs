using ResourcePulse.Domain.Access;

namespace ResourcePulse.Domain.Tests.Access;

// The authorization aggregate (ADR-0030). A membership is granted before its
// holder has ever signed in, so the interesting behaviour is the hand-off from
// "invited by email" to "bound to an identity subject".
public class MembershipTests
{
    [Fact]
    public void Invite_StartsPending_AtTheGrantedRole()
    {
        var m = Membership.Invite(" Anna@Example.com ", AppRole.Planner, "granter-sub");

        m.Email.Should().Be("Anna@Example.com"); // trimmed, case preserved (citext compares)
        m.Role.Should().Be(AppRole.Planner);
        m.UserSub.Should().BeNull();
        m.IsPending.Should().BeTrue();
        m.GrantedByUserSub.Should().Be("granter-sub");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    public void Invite_RejectsAnUnusableAddress(string email) =>
        FluentActions.Invoking(() => Membership.Invite(email, AppRole.Viewer))
            .Should().Throw<DomainException>();

    [Fact]
    public void ClaimBy_BindsTheSubjectAndClearsPending()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Viewer);

        m.ClaimBy("sub-1", "Anna Blu");

        m.UserSub.Should().Be("sub-1");
        m.DisplayName.Should().Be("Anna Blu");
        m.IsPending.Should().BeFalse();
    }

    // Two sign-ins can race on the same invite; the second must not be an error.
    [Fact]
    public void ClaimBy_IsIdempotentForTheSameSubject()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Viewer);
        m.ClaimBy("sub-1", "Anna Blu");

        FluentActions.Invoking(() => m.ClaimBy("sub-1", "Anna Blu")).Should().NotThrow();
        m.UserSub.Should().Be("sub-1");
    }

    // Silently re-pointing a grant would hand one person's access to another.
    [Fact]
    public void ClaimBy_RefusesADifferentSubject()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Owner);
        m.ClaimBy("sub-1");

        FluentActions.Invoking(() => m.ClaimBy("sub-2"))
            .Should().Throw<DomainException>().WithMessage("*already claimed*");
    }

    [Fact]
    public void ChangeRole_IsANoOpForTheSameRole()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Viewer);

        m.ChangeRole(AppRole.Viewer, "granter");

        m.UpdatedAt.Should().BeNull();
        m.GrantedByUserSub.Should().BeNull();
    }

    [Fact]
    public void ChangeRole_RecordsWhoGrantedIt()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Viewer);

        m.ChangeRole(AppRole.Owner, "granter");

        m.Role.Should().Be(AppRole.Owner);
        m.GrantedByUserSub.Should().Be("granter");
        m.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeEmail_IsAllowedWhilePending()
    {
        var m = Membership.Invite("typo@example.com", AppRole.Viewer);

        m.ChangeEmail("anna@example.com");

        m.Email.Should().Be("anna@example.com");
    }

    // Once claimed the subject is the identity; editing the address would be a
    // no-op dressed up as a fix.
    [Fact]
    public void ChangeEmail_IsRefusedOnceClaimed()
    {
        var m = Membership.Invite("anna@example.com", AppRole.Viewer);
        m.ClaimBy("sub-1");

        FluentActions.Invoking(() => m.ChangeEmail("other@example.com"))
            .Should().Throw<DomainException>();
    }

    // The enum's ORDER is the authorization rule — a comparison, never a lookup
    // table that could drift away from it.
    [Theory]
    [InlineData(AppRole.Owner, AppRole.Planner, true)]
    [InlineData(AppRole.Owner, AppRole.Viewer, true)]
    [InlineData(AppRole.Planner, AppRole.Viewer, true)]
    [InlineData(AppRole.Planner, AppRole.Owner, false)]
    [InlineData(AppRole.Viewer, AppRole.Planner, false)]
    public void RoleHierarchy_IsTheEnumOrder(AppRole held, AppRole required, bool satisfied) =>
        (held >= required).Should().Be(satisfied);
}
