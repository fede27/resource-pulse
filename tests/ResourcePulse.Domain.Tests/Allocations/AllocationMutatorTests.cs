using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

public class AllocationMutatorTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node     = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End   = new(2026, 6, 14);

    private static Allocation Fresh(decimal percent = 50m)
    {
        var a = Allocation.Create(Resource, Node, Start, End, percent);
        a.ClearDomainEvents();
        return a;
    }

    // ── ChangePeriod ────────────────────────────────────────────────────────

    [Fact]
    public void ChangePeriod_HappyPath_UpdatesAndRaisesEvent()
    {
        var a = Fresh();
        var newStart = new DateOnly(2026, 7, 1);
        var newEnd = new DateOnly(2026, 7, 31);

        a.ChangePeriod(newStart, newEnd);

        a.PeriodStart.Should().Be(newStart);
        a.PeriodEnd.Should().Be(newEnd);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationPeriodChanged>()
            .Which.Should().Match<AllocationPeriodChanged>(e =>
                e.AllocationId == a.Id &&
                e.OldStart == Start &&
                e.OldEnd == End &&
                e.NewStart == newStart &&
                e.NewEnd == newEnd);
    }

    [Fact]
    public void ChangePeriod_StartAfterEnd_Throws()
    {
        var a = Fresh();

        var act = () => a.ChangePeriod(new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1));

        act.Should().Throw<DomainException>().WithMessage("*PeriodStart must be on or before PeriodEnd*");
    }

    [Fact]
    public void ChangePeriod_NoOp_SuppressesEvent()
    {
        var a = Fresh();

        a.ChangePeriod(Start, End);

        a.DomainEvents.Should().BeEmpty();
    }

    // ── ChangePercent ───────────────────────────────────────────────────────

    [Fact]
    public void ChangePercent_HappyPath_UpdatesAndRaisesEvent()
    {
        var a = Fresh(percent: 50m);

        a.ChangePercent(75m);

        a.AllocationPercent.Should().Be(75m);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationPercentChanged>()
            .Which.Should().Match<AllocationPercentChanged>(e =>
                e.AllocationId == a.Id && e.OldPercent == 50m && e.NewPercent == 75m);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1)]
    [InlineData(1000.01)]
    [InlineData(1001)]
    public void ChangePercent_OutOfRange_Throws(double percent)
    {
        var a = Fresh();

        var act = () => a.ChangePercent((decimal)percent);

        act.Should().Throw<DomainException>().WithMessage("*range (0, 1000]*");
    }

    [Theory]
    [InlineData(150)]
    [InlineData(500)]
    [InlineData(1000)]
    public void ChangePercent_OvercommitmentInRange_OK(double percent)
    {
        var a = Fresh(percent: 50m);

        a.ChangePercent((decimal)percent);

        a.AllocationPercent.Should().Be((decimal)percent);
    }

    [Fact]
    public void ChangePercent_NoOp_SuppressesEvent()
    {
        var a = Fresh(percent: 50m);

        a.ChangePercent(50m);

        a.DomainEvents.Should().BeEmpty();
        a.AllocationPercent.Should().Be(50m);
    }

    // ── Annotate ────────────────────────────────────────────────────────────

    [Fact]
    public void Annotate_SetsNotes_NoEvent()
    {
        var a = Fresh();

        a.Annotate("  some context  ");

        a.Notes.Should().Be("some context");
        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Annotate_BlankNotes_StoresNull()
    {
        var a = Fresh();
        a.Annotate("first");
        a.Annotate("   ");

        a.Notes.Should().BeNull();
    }

    [Fact]
    public void Annotate_Null_StoresNull()
    {
        var a = Fresh();
        a.Annotate("first");
        a.Annotate(null);

        a.Notes.Should().BeNull();
    }

    // ── MarkDeleted ─────────────────────────────────────────────────────────

    [Fact]
    public void MarkDeleted_RaisesEvent()
    {
        var a = Fresh();

        a.MarkDeleted();

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationDeleted>()
            .Which.AllocationId.Should().Be(a.Id);
    }
}
