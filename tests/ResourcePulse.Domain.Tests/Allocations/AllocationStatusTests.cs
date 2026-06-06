using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

public class AllocationStatusTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 14);

    private static Allocation FreshTentative()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);
        a.ClearDomainEvents();
        return a;
    }

    private static Allocation FreshHard()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m, notes: null, status: AllocationStatus.Hard);
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public void ChangeStatus_TentativeToHard_RaisesEvent()
    {
        var a = FreshTentative();

        a.ChangeStatus(AllocationStatus.Hard);

        a.Status.Should().Be(AllocationStatus.Hard);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationStatusChanged>()
            .Which.Should().Match<AllocationStatusChanged>(e =>
                e.AllocationId == a.Id &&
                e.OldStatus == AllocationStatus.Tentative &&
                e.NewStatus == AllocationStatus.Hard &&
                e.Reason == null);
    }

    [Fact]
    public void ChangeStatus_HardToTentative_WithReason_RaisesEventCarryingReason()
    {
        var a = FreshHard();

        a.ChangeStatus(AllocationStatus.Tentative, "ProjectCommitmentDowngrade");

        a.Status.Should().Be(AllocationStatus.Tentative);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationStatusChanged>()
            .Which.Should().Match<AllocationStatusChanged>(e =>
                e.Reason == "ProjectCommitmentDowngrade" &&
                e.OldStatus == AllocationStatus.Hard &&
                e.NewStatus == AllocationStatus.Tentative);
    }

    [Fact]
    public void ChangeStatus_NoOp_SuppressesEvent()
    {
        var a = FreshTentative();

        a.ChangeStatus(AllocationStatus.Tentative);

        a.DomainEvents.Should().BeEmpty();
        a.Status.Should().Be(AllocationStatus.Tentative);
    }

    [Fact]
    public void ChangeStatus_BlankReason_NormalizedToNullInEvent()
    {
        var a = FreshTentative();

        a.ChangeStatus(AllocationStatus.Hard, "   ");

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationStatusChanged>()
            .Which.Reason.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_AppliesAlsoToPlaceholder()
    {
        // I6 is enforced at the service layer. The aggregate accepts either
        // status on either form.
        var a = Allocation.CreatePlaceholder(Node, Start, End, 50m, Guid.NewGuid(), null);
        a.ClearDomainEvents();

        a.ChangeStatus(AllocationStatus.Hard);

        a.Status.Should().Be(AllocationStatus.Hard);
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationStatusChanged>();
    }
}
