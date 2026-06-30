using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeFactoryTests
{
    [Fact]
    public void CreateRoot_EmptyName_Throws()
    {
        var act = () => ProjectNode.CreateRoot("   ", null,
            ProjectType.Internal, CommitmentLevel.Planned, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void CreateRoot_TrimsNameAndCode()
    {
        var node = ProjectNode.CreateRoot("  Apollo  ", "  AP-01  ",
            ProjectType.Customer, CommitmentLevel.Committed, null);

        node.Name.Should().Be("Apollo");
        node.Code.Should().Be("AP-01");
    }

    [Fact]
    public void CreateRoot_BlankCode_StoresNull()
    {
        var node = ProjectNode.CreateRoot("Apollo", "   ",
            ProjectType.Internal, CommitmentLevel.Planned, null);

        node.Code.Should().BeNull();
    }

    [Fact]
    public void CreateRoot_SetsRootDefaults()
    {
        var node = ProjectNode.CreateRoot("Apollo", null,
            ProjectType.Customer, CommitmentLevel.Critical, Guid.NewGuid());

        node.NodeType.Should().Be(ProjectNodeType.Project);
        node.ParentId.Should().BeNull();
        node.Depth.Should().Be(0);
        node.Path.Should().Be("/" + node.Id.ToString("D"));
        node.Status.Should().Be(ProjectStatus.Draft);
        node.Type.Should().Be(ProjectType.Customer);
        node.CommitmentLevel.Should().Be(CommitmentLevel.Critical);
    }

    [Fact]
    public void CreateRoot_EmptyLeadGuid_StoresNull()
    {
        var node = ProjectNode.CreateRoot("Apollo", null,
            ProjectType.Internal, CommitmentLevel.Planned, Guid.Empty);

        node.LeadResourceId.Should().BeNull();
    }

    // ── Client (M1) ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateRoot_WithClient_StoresTrimmed()
    {
        var node = ProjectNode.CreateRoot("Apollo", null,
            ProjectType.Customer, CommitmentLevel.Committed, null, "  ACME S.p.A.  ");

        node.Client.Should().Be("ACME S.p.A.");
    }

    [Fact]
    public void CreateRoot_NoClient_DefaultsToNull()
    {
        var node = ProjectNode.CreateRoot("Apollo", null,
            ProjectType.Internal, CommitmentLevel.Planned, null);

        node.Client.Should().BeNull();
    }

    [Fact]
    public void CreateRoot_BlankClient_StoresNull()
    {
        var node = ProjectNode.CreateRoot("Apollo", null,
            ProjectType.Internal, CommitmentLevel.Planned, null, "   ");

        node.Client.Should().BeNull();
    }

    [Fact]
    public void ChangeClient_OnProject_UpdatesValue()
    {
        var node = Project();

        node.ChangeClient("Globex");
        node.Client.Should().Be("Globex");

        node.ChangeClient("  ");
        node.Client.Should().BeNull(); // blank clears
    }

    [Fact]
    public void ChangeClient_OnNonProject_Throws()
    {
        var phase = Phase(Project());

        var act = () => phase.ChangeClient("ACME");

        act.Should().Throw<DomainException>()
            .WithMessage("*only allowed on Project root nodes*");
    }

    [Fact]
    public void CreateChild_NeverCarriesClient()
    {
        var phase = Phase(Project());

        phase.Client.Should().BeNull();
    }

    [Fact]
    public void CreateChild_PhaseUnderProject_OK()
    {
        var root = Project();
        var phase = Phase(root, "P1");

        phase.NodeType.Should().Be(ProjectNodeType.Phase);
        phase.ParentId.Should().Be(root.Id);
        phase.Depth.Should().Be(1);
        phase.Path.Should().Be(root.Path + "/" + phase.Id.ToString("D"));
        phase.Status.Should().BeNull();
        phase.Type.Should().BeNull();
        phase.CommitmentLevel.Should().BeNull();
    }

    [Fact]
    public void CreateChild_WorkPackageUnderPhase_OK()
    {
        var root = Project();
        var phase = Phase(root);
        var wp = WorkPackage(phase, "WP1");

        wp.NodeType.Should().Be(ProjectNodeType.WorkPackage);
        wp.Depth.Should().Be(2);
        wp.Path.Should().StartWith(phase.Path + "/");
    }

    [Fact]
    public void CreateChild_WorkPackageUnderProject_OK()
    {
        var root = Project();
        var wp = WorkPackage(root, "Direct WP");

        wp.NodeType.Should().Be(ProjectNodeType.WorkPackage);
        wp.Depth.Should().Be(1);
    }

    [Fact]
    public void CreateChild_ProjectUnderProject_Throws()
    {
        var root = Project();

        var act = () => ProjectNode.CreateChild(root, ProjectNodeType.Project, "x", null);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot create a Project root*");
    }

    [Fact]
    public void CreateChild_PhaseUnderPhase_Throws()
    {
        var root = Project();
        var phase = Phase(root);

        var act = () => ProjectNode.CreateChild(phase, ProjectNodeType.Phase, "P2", null);

        act.Should().Throw<DomainException>()
            .WithMessage("*Phase cannot contain a Phase*");
    }

    [Fact]
    public void CreateChild_AnyUnderWorkPackage_Throws()
    {
        var root = Project();
        var wp = WorkPackage(root);

        var act = () => ProjectNode.CreateChild(wp, ProjectNodeType.WorkPackage, "nested", null);

        act.Should().Throw<DomainException>()
            .WithMessage("*WorkPackage cannot contain*");
    }
}
