using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

public class AllocationPlaceholderTransitionTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid Role = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 14);

    // ── ConvertToPlaceholder ────────────────────────────────────────────────

    [Fact]
    public void ConvertToPlaceholder_HappyPath_PreservesIdSpanRate()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 75m, "context", AllocationStatus.Hard);
        a.ClearDomainEvents();
        var originalId = a.Id;

        a.ConvertToPlaceholder(Role, Owner);

        a.Id.Should().Be(originalId);
        a.ResourceId.Should().BeNull();
        a.IsPlaceholder.Should().BeTrue();
        a.RoleId.Should().Be(Role);
        a.OwnerResourceId.Should().Be(Owner);
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.AllocationPercent.Should().Be(75m);
        a.Status.Should().Be(AllocationStatus.Hard); // status preservato
        a.Notes.Should().Be("context");
    }

    [Fact]
    public void ConvertToPlaceholder_RaisesEvent()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);
        a.ClearDomainEvents();

        a.ConvertToPlaceholder(Role, Owner);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationConvertedToPlaceholder>()
            .Which.Should().Match<AllocationConvertedToPlaceholder>(e =>
                e.AllocationId == a.Id &&
                e.OldResourceId == Resource &&
                e.ProjectNodeId == Node &&
                e.RoleId == Role &&
                e.OwnerResourceId == Owner);
    }

    [Fact]
    public void ConvertToPlaceholder_AlreadyPlaceholder_Throws()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 50m, Role, Owner);

        var act = () => a.ConvertToPlaceholder(Guid.NewGuid(), null);

        act.Should().Throw<DomainException>().WithMessage("*already a placeholder*");
    }

    [Fact]
    public void ConvertToPlaceholder_EmptyRole_Throws()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);

        var act = () => a.ConvertToPlaceholder(Guid.Empty, Owner);

        act.Should().Throw<DomainException>().WithMessage("*role*");
    }

    [Fact]
    public void ConvertToPlaceholder_NullOwner_Allowed()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);
        a.ClearDomainEvents();

        a.ConvertToPlaceholder(Role, ownerResourceId: null);

        a.OwnerResourceId.Should().BeNull();
        a.IsPlaceholder.Should().BeTrue();
    }

    // ── AssignTo ────────────────────────────────────────────────────────────

    [Fact]
    public void AssignTo_HappyPath_PreservesIdSpanRateStatus()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 80m, Role, Owner, notes: "ctx",
            status: AllocationStatus.Hard);
        a.ClearDomainEvents();
        var originalId = a.Id;
        var newResource = Guid.NewGuid();

        a.AssignTo(newResource);

        a.Id.Should().Be(originalId);
        a.ResourceId.Should().Be(newResource);
        a.IsPlaceholder.Should().BeFalse();
        a.RoleId.Should().BeNull();
        a.OwnerResourceId.Should().BeNull();
        a.AllocationPercent.Should().Be(80m);
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.Status.Should().Be(AllocationStatus.Hard);
        a.Notes.Should().Be("ctx");
    }

    [Fact]
    public void AssignTo_RaisesEvent()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 50m, Role, Owner);
        var newResource = Guid.NewGuid();

        a.AssignTo(newResource);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PlaceholderAssignedToResource>()
            .Which.Should().Match<PlaceholderAssignedToResource>(e =>
                e.AllocationId == a.Id &&
                e.NewResourceId == newResource &&
                e.ProjectNodeId == Node);
    }

    [Fact]
    public void AssignTo_NotPlaceholder_Throws()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);

        var act = () => a.AssignTo(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*placeholder*");
    }

    [Fact]
    public void AssignTo_EmptyResourceId_Throws()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 50m, Role, Owner);

        var act = () => a.AssignTo(Guid.Empty);

        act.Should().Throw<DomainException>().WithMessage("*resource*");
    }

    // ── Round-trip: identity preserved ──────────────────────────────────────

    [Fact]
    public void Conversion_RoundTrip_PreservesAggregateIdentity()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 60m);
        var originalId = a.Id;

        a.ConvertToPlaceholder(Role, Owner);
        a.AssignTo(Guid.NewGuid());

        a.Id.Should().Be(originalId);
        a.IsPlaceholder.Should().BeFalse();
        a.AllocationPercent.Should().Be(60m);
    }
}
