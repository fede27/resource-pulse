using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

public class AllocationFactoryTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node     = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End   = new(2026, 6, 14);

    [Fact]
    public void Create_HappyPath_SetsAllFields()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m, "  notes  ");

        a.ResourceId.Should().Be(Resource);
        a.ProjectNodeId.Should().Be(Node);
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.AllocationPercent.Should().Be(50m);
        a.Notes.Should().Be("notes"); // trimmed
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_RaisesAllocationCreatedEvent()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 25m);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationCreated>()
            .Which.Should().Match<AllocationCreated>(e =>
                e.AllocationId == a.Id &&
                e.ResourceId == Resource &&
                e.ProjectNodeId == Node &&
                e.PeriodStart == Start &&
                e.PeriodEnd == End &&
                e.AllocationPercent == 25m);
    }

    [Fact]
    public void Create_EmptyResourceId_Throws()
    {
        var act = () => Allocation.Create(Guid.Empty, Node, Start, End, 50m);

        act.Should().Throw<DomainException>().WithMessage("*resource*");
    }

    [Fact]
    public void Create_EmptyProjectNodeId_Throws()
    {
        var act = () => Allocation.Create(Resource, Guid.Empty, Start, End, 50m);

        act.Should().Throw<DomainException>().WithMessage("*project node*");
    }

    [Fact]
    public void Create_StartAfterEnd_Throws()
    {
        var act = () => Allocation.Create(Resource, Node, End, Start, 50m);

        act.Should().Throw<DomainException>().WithMessage("*PeriodStart must be on or before PeriodEnd*");
    }

    [Fact]
    public void Create_StartEqualsEnd_OK_SingleDay()
    {
        var a = Allocation.Create(Resource, Node, Start, Start, 50m);

        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(Start);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(-50)]
    [InlineData(1000.01)]
    [InlineData(1001)]
    [InlineData(5000)]
    public void Create_PercentOutOfRange_Throws(double percent)
    {
        var act = () => Allocation.Create(Resource, Node, Start, End, (decimal)percent);

        act.Should().Throw<DomainException>().WithMessage("*range (0, 1000]*");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]    // overcommitment is legal (see ADR-0013)
    [InlineData(500)]
    [InlineData(1000)]   // boundary
    public void Create_PercentInRange_OK(double percent)
    {
        var a = Allocation.Create(Resource, Node, Start, End, (decimal)percent);

        a.AllocationPercent.Should().Be((decimal)percent);
    }

    [Fact]
    public void Create_NotesNull_StaysNull()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m, null);
        a.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_NotesBlank_StoresNull()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m, "   ");
        a.Notes.Should().BeNull();
    }

    // ── Status (ADR-0015) ───────────────────────────────────────────────────

    [Fact]
    public void Create_DefaultStatus_IsTentative()
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m);
        a.Status.Should().Be(AllocationStatus.Tentative);
    }

    [Theory]
    [InlineData(AllocationStatus.Tentative)]
    [InlineData(AllocationStatus.Hard)]
    public void Create_WithExplicitStatus_StoresIt(AllocationStatus status)
    {
        var a = Allocation.Create(Resource, Node, Start, End, 50m, notes: null, status: status);
        a.Status.Should().Be(status);
    }

    // ── CreatePlaceholder (ADR-0016) ────────────────────────────────────────

    // RoleId targets the Role catalogue (ADR-0021 / M2).
    private static readonly Guid Role = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void CreatePlaceholder_HappyPath_SetsPlaceholderFields()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 25m, Role, Owner);

        a.ResourceId.Should().BeNull();
        a.IsPlaceholder.Should().BeTrue();
        a.RoleId.Should().Be(Role);
        a.OwnerResourceId.Should().Be(Owner);
        a.ProjectNodeId.Should().Be(Node);
        a.AllocationPercent.Should().Be(25m);
        a.Status.Should().Be(AllocationStatus.Tentative);
    }

    [Fact]
    public void CreatePlaceholder_OwnerOptional()
    {
        var a = Allocation.CreatePlaceholder(Node, Start, End, 25m, Role, ownerResourceId: null);

        a.OwnerResourceId.Should().BeNull();
        a.RoleId.Should().Be(Role);
    }

    [Fact]
    public void CreatePlaceholder_EmptyRole_Throws()
    {
        var act = () => Allocation.CreatePlaceholder(Node, Start, End, 25m, Guid.Empty, null);

        act.Should().Throw<DomainException>().WithMessage("*role*");
    }

    [Fact]
    public void CreatePlaceholder_EmptyOwnerWhenProvided_Throws()
    {
        var act = () => Allocation.CreatePlaceholder(Node, Start, End, 25m, Role, Guid.Empty);

        act.Should().Throw<DomainException>().WithMessage("*OwnerResourceId*");
    }

    [Fact]
    public void CreatePlaceholder_DoesNotRaiseAllocationCreatedEvent()
    {
        // Comment in CreatePlaceholder explains: no AllocationCreated event for
        // placeholders. The conversion/assign events carry the lifecycle.
        var a = Allocation.CreatePlaceholder(Node, Start, End, 25m, Role, Owner);

        a.DomainEvents.Should().BeEmpty();
    }
}
