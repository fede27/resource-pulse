using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

// Coverage factory (Phase 5.1, ADR-0025): Allocation.CreateCoverage(demand, node,
// resource, ...). Always a real resource on a demand — no placeholder form.
public class AllocationFactoryTests
{
    private static readonly Guid Demand   = Guid.NewGuid();
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node     = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End   = new(2026, 6, 14);

    [Fact]
    public void CreateCoverage_HappyPath_SetsAllFields()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m, "  notes  ");

        a.DemandId.Should().Be(Demand);
        a.ResourceId.Should().Be(Resource);
        a.ProjectNodeId.Should().Be(Node);
        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.AllocationPercent.Should().Be(50m);
        a.Notes.Should().Be("notes"); // trimmed
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateCoverage_RaisesAllocationCreatedEvent_WithDemand()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 25m);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationCreated>()
            .Which.Should().Match<AllocationCreated>(e =>
                e.AllocationId == a.Id &&
                e.DemandId == Demand &&
                e.ResourceId == Resource &&
                e.ProjectNodeId == Node &&
                e.PeriodStart == Start &&
                e.PeriodEnd == End &&
                e.AllocationPercent == 25m);
    }

    [Fact]
    public void CreateCoverage_EmptyDemandId_Throws()
    {
        var act = () => Allocation.CreateCoverage(Guid.Empty, Node, Resource, Start, End, 50m);
        act.Should().Throw<DomainException>().WithMessage("*demand*");
    }

    [Fact]
    public void CreateCoverage_EmptyResourceId_Throws()
    {
        var act = () => Allocation.CreateCoverage(Demand, Node, Guid.Empty, Start, End, 50m);
        act.Should().Throw<DomainException>().WithMessage("*resource*");
    }

    [Fact]
    public void CreateCoverage_EmptyProjectNodeId_Throws()
    {
        var act = () => Allocation.CreateCoverage(Demand, Guid.Empty, Resource, Start, End, 50m);
        act.Should().Throw<DomainException>().WithMessage("*project node*");
    }

    [Fact]
    public void CreateCoverage_StartAfterEnd_Throws()
    {
        var act = () => Allocation.CreateCoverage(Demand, Node, Resource, End, Start, 50m);
        act.Should().Throw<DomainException>().WithMessage("*PeriodStart must be on or before PeriodEnd*");
    }

    [Fact]
    public void CreateCoverage_StartEqualsEnd_OK_SingleDay()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, Start, 50m);
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
    public void CreateCoverage_PercentOutOfRange_Throws(double percent)
    {
        var act = () => Allocation.CreateCoverage(Demand, Node, Resource, Start, End, (decimal)percent);
        act.Should().Throw<DomainException>().WithMessage("*range (0, 1000]*");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]    // overcommitment is legal (see ADR-0013)
    [InlineData(500)]
    [InlineData(1000)]   // boundary
    public void CreateCoverage_PercentInRange_OK(double percent)
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, (decimal)percent);
        a.AllocationPercent.Should().Be((decimal)percent);
    }

    [Fact]
    public void CreateCoverage_NotesNull_StaysNull()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m, null);
        a.Notes.Should().BeNull();
    }

    [Fact]
    public void CreateCoverage_NotesBlank_StoresNull()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m, "   ");
        a.Notes.Should().BeNull();
    }

    // ── Status (ADR-0015) ───────────────────────────────────────────────────

    [Fact]
    public void CreateCoverage_DefaultStatus_IsTentative()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m);
        a.Status.Should().Be(AllocationStatus.Tentative);
    }

    [Theory]
    [InlineData(AllocationStatus.Tentative)]
    [InlineData(AllocationStatus.Hard)]
    public void CreateCoverage_WithExplicitStatus_StoresIt(AllocationStatus status)
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m, notes: null, status: status);
        a.Status.Should().Be(status);
    }

    // ── Reassign & RetargetToDemand (amendment C1) ──────────────────────────

    [Fact]
    public void Reassign_SwapsResource_SameDemand()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m);
        a.ClearDomainEvents();
        var other = Guid.NewGuid();

        a.Reassign(other);

        a.ResourceId.Should().Be(other);
        a.DemandId.Should().Be(Demand);
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationResourceChanged>();
    }

    [Fact]
    public void RetargetToDemand_RepointsDemandAndNode_SameResource()
    {
        var a = Allocation.CreateCoverage(Demand, Node, Resource, Start, End, 50m);
        a.ClearDomainEvents();
        var newDemand = Guid.NewGuid();
        var newNode = Guid.NewGuid(); a.Reassign(newNode);

        a.RetargetToDemand(newDemand, newNode);

        a.DemandId.Should().Be(newDemand);
        a.ProjectNodeId.Should().Be(newNode);
        a.ResourceId.Should().Be(Resource);
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AllocationRetargeted>();
    }
}
