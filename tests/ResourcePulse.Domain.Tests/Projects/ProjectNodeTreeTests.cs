using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeTreeTests
{
    [Fact]
    public void Reparent_PhaseAcrossProjects_NotAllowed()
    {
        // Phase belongs to project A; reparenting under project B's phase would put it
        // under a Phase, which is not a valid parent type for a Phase.
        var a = Project("A");
        var phaseInA = Phase(a, "P1");
        var b = Project("B");
        var phaseInB = Phase(b, "Q1");

        var act = () => phaseInA.Reparent(phaseInB, Array.Empty<ProjectNode>());

        act.Should().Throw<DomainException>()
            .WithMessage("*Phase cannot contain a Phase*");
    }

    [Fact]
    public void Reparent_WorkPackage_FromPhase_ToAnotherPhase_UpdatesPath()
    {
        var root = Project("X");
        var p1 = Phase(root, "P1");
        var p2 = Phase(root, "P2");
        var wp = WorkPackage(p1, "WP");

        wp.Reparent(p2, Array.Empty<ProjectNode>());

        wp.ParentId.Should().Be(p2.Id);
        wp.Path.Should().Be(p2.Path + "/" + wp.Id.ToString("D"));
        wp.Depth.Should().Be(2);
    }

    [Fact]
    public void Reparent_SubtreeWithDescendants_UpdatesAllPaths()
    {
        // Move Phase P1 (with its two WorkPackage descendants) from Project A directly
        // under Project B. Valid: a Project can contain a Phase.
        var rootA = Project("A");
        var p1 = Phase(rootA, "P1");
        var wp1 = WorkPackage(p1, "WP1");
        var wp2 = WorkPackage(p1, "WP2");

        var rootB = Project("B");

        p1.Reparent(rootB, new[] { wp1, wp2 });

        p1.ParentId.Should().Be(rootB.Id);
        p1.Depth.Should().Be(1);
        p1.Path.Should().Be(rootB.Path + "/" + p1.Id.ToString("D"));
        wp1.Path.Should().StartWith(p1.Path + "/");
        wp1.Depth.Should().Be(2);
        wp2.Path.Should().StartWith(p1.Path + "/");
        wp2.Depth.Should().Be(2);
    }

    [Fact]
    public void Reparent_OntoOwnDescendant_Throws()
    {
        // Tree: root → P1 → WP. Try to reparent P1 under WP. WP is P1's descendant.
        // The cycle guard fires before the type rule (intentional ordering).
        var root = Project();
        var p1 = Phase(root);
        var wp = WorkPackage(p1);

        var act = () => p1.Reparent(wp, new[] { wp });

        act.Should().Throw<DomainException>()
            .WithMessage("*under own descendant*");
    }

    [Fact]
    public void Reparent_OntoSelf_Throws()
    {
        var root = Project();
        var phase = Phase(root);

        var act = () => phase.Reparent(phase, Array.Empty<ProjectNode>());

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot reparent under self*");
    }

    [Fact]
    public void Reparent_Project_Throws()
    {
        var rootA = Project("A");
        var rootB = Project("B");

        var act = () => rootA.Reparent(rootB, Array.Empty<ProjectNode>());

        act.Should().Throw<DomainException>()
            .WithMessage("*Project root nodes cannot be reparented*");
    }

    [Fact]
    public void Reparent_DescendantsListIncludesForeignNode_Throws()
    {
        // Valid type-wise: move WP from Phase A to Phase B. WP is a leaf so the
        // descendants set should be empty; passing a foreign node triggers the
        // path-prefix sanity check inside Reparent.
        var rootA = Project("A");
        var pA = Phase(rootA, "PA");
        var wp = WorkPackage(pA, "WP");

        var rootB = Project("B");
        var pB = Phase(rootB, "PB");

        var foreign = WorkPackage(Phase(Project("C"), "PC"), "WPC");

        var act = () => wp.Reparent(pB, new[] { foreign });

        act.Should().Throw<DomainException>()
            .WithMessage("*not a descendant*");
    }

    [Fact]
    public void Reparent_NullNewParent_Throws()
    {
        var root = Project();
        var phase = Phase(root);

        var act = () => phase.Reparent(null!, Array.Empty<ProjectNode>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reparent_NullDescendants_Throws()
    {
        var root = Project();
        var p1 = Phase(root, "P1");
        var rootB = Project("B");
        var p2 = Phase(rootB, "P2");

        var act = () => p1.Reparent(p2, null!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Path_Format_IsForwardSlashSeparatedGuids()
    {
        var root = Project();
        var phase = Phase(root);
        var wp = WorkPackage(phase);

        root.Path.Should().Be("/" + root.Id.ToString("D"));
        phase.Path.Should().Be(root.Path + "/" + phase.Id.ToString("D"));
        wp.Path.Should().Be(phase.Path + "/" + wp.Id.ToString("D"));
    }
}
