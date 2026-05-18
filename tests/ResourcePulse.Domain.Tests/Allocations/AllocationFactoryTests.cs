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
    [InlineData(100.01)]
    [InlineData(101)]
    [InlineData(150)]
    public void Create_PercentOutOfRange_Throws(double percent)
    {
        var act = () => Allocation.Create(Resource, Node, Start, End, (decimal)percent);

        act.Should().Throw<DomainException>().WithMessage("*range (0, 100]*");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50)]
    [InlineData(100)]
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
}
