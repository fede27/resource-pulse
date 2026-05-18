using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodePlanningModeTests
{
    // ── Factory defaults ────────────────────────────────────────────────────

    [Fact]
    public void CreateRoot_DefaultsToUnspecified()
    {
        var root = Project();

        root.PlanningMode.Should().Be(PlanningMode.Unspecified);
        root.EstimatedWork.Should().BeNull();
    }

    [Fact]
    public void CreateChild_Phase_DefaultsToUnspecified()
    {
        var root = Project();
        var phase = Phase(root);

        phase.PlanningMode.Should().Be(PlanningMode.Unspecified);
        phase.EstimatedWork.Should().BeNull();
    }

    [Fact]
    public void CreateChild_WorkPackage_PlanningModeNull()
    {
        var root = Project();
        var wp = WorkPackage(root);

        wp.PlanningMode.Should().BeNull();
        wp.EstimatedWork.Should().BeNull();
    }

    // ── SetPlanningMode ─────────────────────────────────────────────────────

    [Fact]
    public void SetPlanningMode_FixedWork_RequiresEstimatedWork()
    {
        var root = Project();

        var act = () => root.SetPlanningMode(PlanningMode.FixedWork, estimatedWork: null);

        act.Should().Throw<DomainException>().WithMessage("*FixedWork requires*EstimatedWork*");
    }

    [Fact]
    public void SetPlanningMode_FixedWork_NonPositiveEstimate_Throws()
    {
        var root = Project();

        var act = () => root.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.Zero);

        act.Should().Throw<DomainException>().WithMessage("*EstimatedWork must be greater than zero*");
    }

    [Fact]
    public void SetPlanningMode_FixedWork_HappyPath()
    {
        var root = Project();

        root.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.FromHours(80));

        root.PlanningMode.Should().Be(PlanningMode.FixedWork);
        root.EstimatedWork.Should().Be(TimeSpan.FromHours(80));
    }

    [Fact]
    public void SetPlanningMode_FixedDuration_RejectsEstimatedWork()
    {
        var root = Project();

        var act = () => root.SetPlanningMode(PlanningMode.FixedDuration, TimeSpan.FromHours(40));

        act.Should().Throw<DomainException>().WithMessage("*only valid when PlanningMode is FixedWork*");
    }

    [Fact]
    public void SetPlanningMode_FixedDuration_NullEstimate_OK()
    {
        var root = Project();

        root.SetPlanningMode(PlanningMode.FixedDuration);

        root.PlanningMode.Should().Be(PlanningMode.FixedDuration);
        root.EstimatedWork.Should().BeNull();
    }

    [Fact]
    public void SetPlanningMode_Unspecified_ClearsEstimatedWork()
    {
        var root = Project();
        root.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.FromHours(40));

        root.SetPlanningMode(PlanningMode.Unspecified);

        root.PlanningMode.Should().Be(PlanningMode.Unspecified);
        root.EstimatedWork.Should().BeNull();
    }

    [Fact]
    public void SetPlanningMode_OnWorkPackage_Throws()
    {
        var root = Project();
        var wp = WorkPackage(root);

        var act = () => wp.SetPlanningMode(PlanningMode.FixedDuration);

        act.Should().Throw<DomainException>().WithMessage("*WorkPackage*");
    }

    [Fact]
    public void SetPlanningMode_OnPhase_OK()
    {
        var root = Project();
        var phase = Phase(root);

        phase.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.FromHours(120));

        phase.PlanningMode.Should().Be(PlanningMode.FixedWork);
        phase.EstimatedWork.Should().Be(TimeSpan.FromHours(120));
    }

    [Fact]
    public void SetPlanningMode_InvalidEnum_Throws()
    {
        var root = Project();

        var act = () => root.SetPlanningMode((PlanningMode)99);

        act.Should().Throw<DomainException>().WithMessage("*Invalid planning mode*");
    }

    // ── UpdateEstimatedWork ─────────────────────────────────────────────────

    [Fact]
    public void UpdateEstimatedWork_FixedWork_HappyPath()
    {
        var root = Project();
        root.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.FromHours(80));

        root.UpdateEstimatedWork(TimeSpan.FromHours(120));

        root.EstimatedWork.Should().Be(TimeSpan.FromHours(120));
    }

    [Fact]
    public void UpdateEstimatedWork_NotFixedWork_Throws()
    {
        var root = Project();
        root.SetPlanningMode(PlanningMode.FixedDuration);

        var act = () => root.UpdateEstimatedWork(TimeSpan.FromHours(40));

        act.Should().Throw<DomainException>().WithMessage("*only valid when PlanningMode is FixedWork*");
    }

    [Fact]
    public void UpdateEstimatedWork_NonPositive_Throws()
    {
        var root = Project();
        root.SetPlanningMode(PlanningMode.FixedWork, TimeSpan.FromHours(40));

        var act = () => root.UpdateEstimatedWork(TimeSpan.Zero);

        act.Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void UpdateEstimatedWork_OnWorkPackage_Throws()
    {
        var root = Project();
        var wp = WorkPackage(root);

        var act = () => wp.UpdateEstimatedWork(TimeSpan.FromHours(10));

        act.Should().Throw<DomainException>().WithMessage("*WorkPackage*");
    }
}
