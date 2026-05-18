using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeStateTransitionTests
{
    [Fact]
    public void NewProject_StartsInDraft()
    {
        var p = Project();
        p.Status.Should().Be(ProjectStatus.Draft);
        p.ActualStart.Should().BeNull();
        p.ActualEnd.Should().BeNull();
    }

    [Fact]
    public void Start_FromDraft_GoesToActive_AndSetsActualStart()
    {
        var p = Project();

        p.Start();

        p.Status.Should().Be(ProjectStatus.Active);
        p.ActualStart.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void Start_FromActive_Throws()
    {
        var p = Project();
        p.Start();

        var act = () => p.Start();

        act.Should().Throw<DomainException>().WithMessage("*Cannot Start from status Active*");
    }

    [Fact]
    public void Start_FromOnHold_Throws()
    {
        // Per the agreed deviation: Start is only valid from Draft; OnHold returns via Resume.
        var p = Project();
        p.Start();
        p.Suspend("waiting on legal");

        var act = () => p.Start();

        act.Should().Throw<DomainException>().WithMessage("*Cannot Start from status OnHold*");
    }

    [Fact]
    public void Complete_FromActive_SetsActualEnd_AndClosed()
    {
        var p = Project();
        p.Start();

        p.Complete();

        p.Status.Should().Be(ProjectStatus.Closed);
        p.ActualEnd.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void Complete_FromDraft_Throws()
    {
        var p = Project();

        var act = () => p.Complete();

        act.Should().Throw<DomainException>().WithMessage("*Cannot Complete from status Draft*");
    }

    [Fact]
    public void Suspend_FromActive_GoesToOnHold()
    {
        var p = Project();
        p.Start();

        p.Suspend("waiting");

        p.Status.Should().Be(ProjectStatus.OnHold);
    }

    [Fact]
    public void Suspend_FromDraft_Throws()
    {
        var p = Project();

        var act = () => p.Suspend("reason");

        act.Should().Throw<DomainException>().WithMessage("*Cannot Suspend from status Draft*");
    }

    [Fact]
    public void Suspend_BlankReason_Throws()
    {
        var p = Project();
        p.Start();

        var act = () => p.Suspend("   ");

        act.Should().Throw<DomainException>().WithMessage("*requires a reason*");
    }

    [Fact]
    public void Resume_FromOnHold_GoesToActive()
    {
        var p = Project();
        p.Start();
        p.Suspend("waiting");

        p.Resume();

        p.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Resume_FromActive_Throws()
    {
        var p = Project();
        p.Start();

        var act = () => p.Resume();

        act.Should().Throw<DomainException>().WithMessage("*Cannot Resume from status Active*");
    }

    [Fact]
    public void Cancel_FromDraft_GoesToCancelled()
    {
        var p = Project();

        p.Cancel("not proceeding");

        p.Status.Should().Be(ProjectStatus.Cancelled);
        p.ActualEnd.Should().BeNull(); // cancellation is not completion
    }

    [Fact]
    public void Cancel_FromOnHold_GoesToCancelled()
    {
        var p = Project();
        p.Start();
        p.Suspend("waiting");

        p.Cancel("scrapped");

        p.Status.Should().Be(ProjectStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromClosed_Throws()
    {
        var p = Project();
        p.Start();
        p.Complete();

        var act = () => p.Cancel("late cancel");

        act.Should().Throw<DomainException>().WithMessage("*Cannot Cancel from status Closed*");
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var p = Project();
        p.Cancel("never starting");

        var act = () => p.Cancel("again");

        act.Should().Throw<DomainException>().WithMessage("*Cannot Cancel from status Cancelled*");
    }

    [Fact]
    public void StateTransitions_OnNonProject_Throws()
    {
        var root = Project();
        var phase = Phase(root);

        ((Action)(() => phase.Start())).Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
        ((Action)(() => phase.Complete())).Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
        ((Action)(() => phase.Suspend("x"))).Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
        ((Action)(() => phase.Resume())).Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
        ((Action)(() => phase.Cancel("x"))).Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
    }
}
