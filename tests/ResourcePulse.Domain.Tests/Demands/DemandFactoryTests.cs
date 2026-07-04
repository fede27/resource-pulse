using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Demands;

public class DemandFactoryTests
{
    private static readonly Guid Node  = Guid.NewGuid();
    private static readonly Guid Role  = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Create_HappyPath_SetsAllFields()
    {
        var d = Demand.Create(Node, Role, TimeSpan.FromHours(40), DemandProvenance.Declared, Owner, "  notes  ");

        d.ProjectNodeId.Should().Be(Node);
        d.RoleId.Should().Be(Role);
        d.RequiredHours.Should().Be(TimeSpan.FromHours(40));
        d.Provenance.Should().Be(DemandProvenance.Declared);
        d.OwnerResourceId.Should().Be(Owner);
        d.Notes.Should().Be("notes"); // trimmed
        d.Id.Should().NotBe(Guid.Empty);
        d.IsBestEffort.Should().BeFalse();
    }

    [Fact]
    public void Create_RaisesDemandCreatedEvent()
    {
        var d = Demand.Create(Node, Role, TimeSpan.FromHours(40), DemandProvenance.Inferred, Owner);

        d.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DemandCreated>()
            .Which.Should().Match<DemandCreated>(e =>
                e.DemandId == d.Id &&
                e.ProjectNodeId == Node &&
                e.RoleId == Role &&
                e.RequiredHours == TimeSpan.FromHours(40) &&
                e.Provenance == DemandProvenance.Inferred &&
                e.OwnerResourceId == Owner);
    }

    [Fact]
    public void Create_NullRequiredHours_IsBestEffort()
    {
        var d = Demand.Create(Node, Role, requiredHours: null, DemandProvenance.Declared);

        d.RequiredHours.Should().BeNull();
        d.IsBestEffort.Should().BeTrue();
    }

    [Fact]
    public void Create_OwnerOptional()
    {
        var d = Demand.Create(Node, Role, TimeSpan.FromHours(8), DemandProvenance.Declared, ownerResourceId: null);
        d.OwnerResourceId.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyNode_Throws()
    {
        var act = () => Demand.Create(Guid.Empty, Role, null, DemandProvenance.Declared);
        act.Should().Throw<DomainException>().WithMessage("*project node*");
    }

    [Fact]
    public void Create_EmptyRole_Throws()
    {
        var act = () => Demand.Create(Node, Guid.Empty, null, DemandProvenance.Declared);
        act.Should().Throw<DomainException>().WithMessage("*role*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveRequiredHours_Throws(int hours)
    {
        var act = () => Demand.Create(Node, Role, TimeSpan.FromHours(hours), DemandProvenance.Declared);
        act.Should().Throw<DomainException>().WithMessage("*RequiredHours*greater than zero*");
    }

    [Fact]
    public void Create_EmptyOwnerWhenProvided_Throws()
    {
        var act = () => Demand.Create(Node, Role, null, DemandProvenance.Declared, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("*OwnerResourceId*");
    }

    [Fact]
    public void Create_UnknownProvenance_Throws()
    {
        var act = () => Demand.Create(Node, Role, null, (DemandProvenance)99);
        act.Should().Throw<DomainException>().WithMessage("*provenance*");
    }

    [Fact]
    public void Create_NotesBlank_StoresNull()
    {
        var d = Demand.Create(Node, Role, null, DemandProvenance.Declared, null, "   ");
        d.Notes.Should().BeNull();
    }
}
