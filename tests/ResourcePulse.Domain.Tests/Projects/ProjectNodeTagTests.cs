using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeTagTests
{
    [Fact]
    public void AddTag_OnRoot_OK()
    {
        var p = Project();
        var tagId = Guid.NewGuid();

        p.AddTag(tagId);

        p.Tags.Should().ContainSingle().Which.TagId.Should().Be(tagId);
    }

    [Fact]
    public void AddTag_OnPhase_OK()
    {
        var p = Project();
        var phase = Phase(p);
        var tagId = Guid.NewGuid();

        phase.AddTag(tagId);

        phase.Tags.Should().ContainSingle().Which.TagId.Should().Be(tagId);
    }

    [Fact]
    public void AddTag_OnWorkPackage_OK()
    {
        var p = Project();
        var wp = WorkPackage(p);
        var tagId = Guid.NewGuid();

        wp.AddTag(tagId);

        wp.Tags.Should().ContainSingle().Which.TagId.Should().Be(tagId);
    }

    [Fact]
    public void AddTag_Duplicate_Throws()
    {
        var p = Project();
        var tagId = Guid.NewGuid();
        p.AddTag(tagId);

        var act = () => p.AddTag(tagId);

        act.Should().Throw<DomainException>().WithMessage("*already has tag*");
    }

    [Fact]
    public void RemoveTag_RemovesEntry()
    {
        var p = Project();
        var tagId = Guid.NewGuid();
        p.AddTag(tagId);

        p.RemoveTag(tagId);

        p.Tags.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_Missing_Throws()
    {
        var p = Project();

        var act = () => p.RemoveTag(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*does not have tag*");
    }

    [Fact]
    public void EmptyTagId_Throws()
    {
        var p = Project();

        var act = () => p.AddTag(Guid.Empty);

        act.Should().Throw<DomainException>().WithMessage("*must reference a tag*");
    }
}
