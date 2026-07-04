using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Demands;

public class DemandMutatorTests
{
    private static readonly Guid Node  = Guid.NewGuid();
    private static readonly Guid Role  = Guid.NewGuid();
    private static readonly Guid Role2 = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static Demand Seed(TimeSpan? required = null, Guid? owner = null) =>
        NoEvents(Demand.Create(Node, Role, required, DemandProvenance.Declared, owner));

    // Clears the creation event so mutator assertions see only their own event.
    private static Demand NoEvents(Demand d) { d.ClearDomainEvents(); return d; }

    // ── ChangeRequiredHours ──────────────────────────────────────────────────

    [Fact]
    public void ChangeRequiredHours_SetsValue_AndRaisesEvent()
    {
        var d = Seed();
        d.ChangeRequiredHours(TimeSpan.FromHours(20));

        d.RequiredHours.Should().Be(TimeSpan.FromHours(20));
        d.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DemandRequiredHoursChanged>()
            .Which.Should().Match<DemandRequiredHoursChanged>(e =>
                e.OldRequiredHours == null && e.NewRequiredHours == TimeSpan.FromHours(20));
    }

    [Fact]
    public void ChangeRequiredHours_ToNull_MovesToBestEffort()
    {
        var d = Seed(TimeSpan.FromHours(20));
        d.ChangeRequiredHours(null);

        d.RequiredHours.Should().BeNull();
        d.IsBestEffort.Should().BeTrue();
        d.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DemandRequiredHoursChanged>();
    }

    [Fact]
    public void ChangeRequiredHours_NoOp_SuppressesEvent()
    {
        var d = Seed(TimeSpan.FromHours(20));
        d.ChangeRequiredHours(TimeSpan.FromHours(20));
        d.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeRequiredHours_NonPositive_Throws()
    {
        var d = Seed();
        var act = () => d.ChangeRequiredHours(TimeSpan.Zero);
        act.Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    // ── ChangeOwner ──────────────────────────────────────────────────────────

    [Fact]
    public void ChangeOwner_SetsAndClears_WithEvents()
    {
        var d = Seed();
        d.ChangeOwner(Owner);
        d.OwnerResourceId.Should().Be(Owner);
        d.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DemandOwnerChanged>();

        NoEvents(d).ChangeOwner(null);
        d.OwnerResourceId.Should().BeNull();
        d.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DemandOwnerChanged>();
    }

    [Fact]
    public void ChangeOwner_NoOp_SuppressesEvent()
    {
        var d = Seed(owner: Owner);
        d.ChangeOwner(Owner);
        d.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeOwner_EmptyGuid_Throws()
    {
        var d = Seed();
        var act = () => d.ChangeOwner(Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("*OwnerResourceId*");
    }

    // ── ChangeRole (amendment C2) ────────────────────────────────────────────

    [Fact]
    public void ChangeRole_CorrectsRole_AndRaisesEvent()
    {
        var d = Seed();
        d.ChangeRole(Role2);

        d.RoleId.Should().Be(Role2);
        d.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DemandRoleChanged>()
            .Which.Should().Match<DemandRoleChanged>(e => e.OldRoleId == Role && e.NewRoleId == Role2);
    }

    [Fact]
    public void ChangeRole_NoOp_SuppressesEvent()
    {
        var d = Seed();
        d.ChangeRole(Role);
        d.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeRole_EmptyGuid_Throws()
    {
        var d = Seed();
        var act = () => d.ChangeRole(Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("*role*");
    }

    // ── Annotate & MarkDeleted ───────────────────────────────────────────────

    [Fact]
    public void Annotate_RaisesNoEvent()
    {
        var d = Seed();
        d.Annotate("note");
        d.Notes.Should().Be("note");
        d.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkDeleted_RaisesDemandDeleted()
    {
        var d = Seed();
        d.MarkDeleted();
        d.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DemandDeleted>();
    }
}
