using ResourcePulse.Domain.Events;
using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeEventsTests
{
    [Fact]
    public void CreateRoot_RaisesProjectNodeCreated()
    {
        var p = Project();

        p.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProjectNodeCreated>()
            .Which.Should().Match<ProjectNodeCreated>(e =>
                e.NodeId == p.Id &&
                e.ParentId == null &&
                e.NodeType == ProjectNodeType.Project);
    }

    [Fact]
    public void CreateChild_RaisesProjectNodeCreated_WithParent()
    {
        var p = Project();
        var phase = Phase(p);

        phase.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProjectNodeCreated>()
            .Which.ParentId.Should().Be(p.Id);
    }

    [Fact]
    public void Start_RaisesProjectStatusChanged_DraftToActive()
    {
        var p = Project();
        p.ClearDomainEvents();

        p.Start();

        p.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProjectStatusChanged>()
            .Which.Should().Match<ProjectStatusChanged>(e =>
                e.From == ProjectStatus.Draft && e.To == ProjectStatus.Active);
    }

    [Fact]
    public void Baseline_RaisesProjectBaselined_NotRebaseline()
    {
        var p = Project();
        p.ClearDomainEvents();

        p.Baseline(new(2026, 6, 1), new(2026, 8, 31));

        var evt = p.DomainEvents.OfType<ProjectBaselined>().Should().ContainSingle().Subject;
        evt.IsRebaseline.Should().BeFalse();
    }

    [Fact]
    public void Rebaseline_RaisesProjectBaselined_AsRebaseline()
    {
        var p = Project();
        p.Baseline(new(2026, 6, 1), new(2026, 8, 31));
        p.ClearDomainEvents();

        p.Rebaseline(new(2026, 6, 1), new(2026, 9, 30), "scope changed");

        var evt = p.DomainEvents.OfType<ProjectBaselined>().Should().ContainSingle().Subject;
        evt.IsRebaseline.Should().BeTrue();
    }

    [Fact]
    public void Replan_RaisesProjectReplanned_OnFirstSet()
    {
        var p = Project();
        p.ClearDomainEvents();

        p.Replan(new(2026, 6, 1), new(2026, 8, 31));

        p.DomainEvents.OfType<ProjectReplanned>().Should().ContainSingle();
    }

    [Fact]
    public void Replan_NoChange_DoesNotRaiseEvent()
    {
        var p = Project();
        p.Replan(new(2026, 6, 1), new(2026, 8, 31));
        p.ClearDomainEvents();

        p.Replan(new(2026, 6, 1), new(2026, 8, 31));

        p.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reparent_RaisesProjectNodeReparented_WithOldAndNewParent()
    {
        var rootA = Project("A");
        var p1 = Phase(rootA);
        var rootB = Project("B");
        var p2 = Phase(rootB);
        var wp = WorkPackage(p1);
        wp.ClearDomainEvents();

        wp.Reparent(p2, Array.Empty<ProjectNode>());

        var evt = wp.DomainEvents.OfType<ProjectNodeReparented>().Should().ContainSingle().Subject;
        evt.OldParentId.Should().Be(p1.Id);
        evt.NewParentId.Should().Be(p2.Id);
    }
}
