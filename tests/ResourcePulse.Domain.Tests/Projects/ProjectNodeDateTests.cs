using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeDateTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D2 = new(2026, 8, 31);
    private static readonly DateOnly D3 = new(2026, 9, 30);

    [Fact]
    public void Baseline_SetsDates_AndTimestamp()
    {
        var p = Project();

        p.Baseline(D1, D2);

        p.BaselineStart.Should().Be(D1);
        p.BaselineEnd.Should().Be(D2);
        p.BaselinedAt.Should().NotBeNull();
    }

    [Fact]
    public void Baseline_StartAfterEnd_Throws()
    {
        var p = Project();

        var act = () => p.Baseline(D2, D1);

        act.Should().Throw<DomainException>().WithMessage("*BaselineStart must be on or before BaselineEnd*");
    }

    [Fact]
    public void Baseline_Twice_Throws()
    {
        var p = Project();
        p.Baseline(D1, D2);

        var act = () => p.Baseline(D1, D3);

        act.Should().Throw<DomainException>().WithMessage("*already baselined*");
    }

    [Fact]
    public void Rebaseline_WithoutInitialBaseline_Throws()
    {
        var p = Project();

        var act = () => p.Rebaseline(D1, D2, "scope changed");

        act.Should().Throw<DomainException>().WithMessage("*has not been baselined*");
    }

    [Fact]
    public void Rebaseline_OverwritesDates()
    {
        var p = Project();
        p.Baseline(D1, D2);

        p.Rebaseline(D1, D3, "scope changed");

        p.BaselineEnd.Should().Be(D3);
    }

    [Fact]
    public void Rebaseline_BlankReason_Throws()
    {
        var p = Project();
        p.Baseline(D1, D2);

        var act = () => p.Rebaseline(D1, D3, "  ");

        act.Should().Throw<DomainException>().WithMessage("*requires a reason*");
    }

    [Fact]
    public void Replan_SetsBothEnds()
    {
        var p = Project();

        p.Replan(D1, D2);

        p.PlannedStart.Should().Be(D1);
        p.PlannedEnd.Should().Be(D2);
    }

    [Fact]
    public void Replan_OneSidedIsAllowed()
    {
        var p = Project();

        p.Replan(D1, null);

        p.PlannedStart.Should().Be(D1);
        p.PlannedEnd.Should().BeNull();
    }

    [Fact]
    public void Replan_StartAfterEnd_Throws()
    {
        var p = Project();

        var act = () => p.Replan(D2, D1);

        act.Should().Throw<DomainException>().WithMessage("*PlannedStart must be on or before PlannedEnd*");
    }

    [Fact]
    public void BackfillActuals_PastDates_OK()
    {
        var p = Project();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var later = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        p.BackfillActuals(past, later);

        p.ActualStart.Should().Be(past);
        p.ActualEnd.Should().Be(later);
    }

    [Fact]
    public void BackfillActuals_FutureStart_Throws()
    {
        var p = Project();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var act = () => p.BackfillActuals(future, null);

        act.Should().Throw<DomainException>().WithMessage("*cannot be in the future*");
    }

    [Fact]
    public void BackfillActuals_EndWithoutStart_Throws()
    {
        var p = Project();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        var act = () => p.BackfillActuals(null, past);

        act.Should().Throw<DomainException>().WithMessage("*requires ActualStart*");
    }

    [Fact]
    public void DateMethods_OnWorkPackage_Throw()
    {
        var p = Project();
        var wp = WorkPackage(p);

        ((Action)(() => wp.Baseline(D1, D2))).Should().Throw<DomainException>().WithMessage("*not allowed on WorkPackage*");
        ((Action)(() => wp.Replan(D1, D2))).Should().Throw<DomainException>().WithMessage("*not allowed on WorkPackage*");
        ((Action)(() => wp.BackfillActuals(D1, D2))).Should().Throw<DomainException>().WithMessage("*not allowed on WorkPackage*");
    }

    [Fact]
    public void DateMethods_OnPhase_AreAllowed()
    {
        var p = Project();
        var phase = Phase(p);

        ((Action)(() => phase.Baseline(D1, D2))).Should().NotThrow();
        ((Action)(() => phase.Replan(D1, D3))).Should().NotThrow();
    }

    [Fact]
    public void RecalculatePlannedFromChildren_TwoPhases_RollsUpToProject()
    {
        var p = Project();
        var ph1 = Phase(p, "P1");
        var ph2 = Phase(p, "P2");
        ph1.Replan(D1, D2);
        ph2.Replan(D2, D3);

        p.RecalculatePlannedFromChildren(new[] { ph1, ph2 });

        p.PlannedStart.Should().Be(D1);
        p.PlannedEnd.Should().Be(D3);
    }

    [Fact]
    public void RecalculatePlannedFromChildren_NoChildren_NoOp()
    {
        var p = Project();

        p.RecalculatePlannedFromChildren(Array.Empty<ProjectNode>());

        p.PlannedStart.Should().BeNull();
        p.PlannedEnd.Should().BeNull();
    }

    [Fact]
    public void RecalculateBaselineFromChildren_RollsUp()
    {
        var p = Project();
        var ph1 = Phase(p, "P1");
        var ph2 = Phase(p, "P2");
        ph1.Baseline(D1, D2);
        ph2.Baseline(D2, D3);

        p.RecalculateBaselineFromChildren(new[] { ph1, ph2 });

        p.BaselineStart.Should().Be(D1);
        p.BaselineEnd.Should().Be(D3);
        p.BaselinedAt.Should().NotBeNull();
    }
}
