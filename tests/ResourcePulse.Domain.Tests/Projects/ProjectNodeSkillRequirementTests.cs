using static ResourcePulse.Domain.Tests.Builders.ProjectNodes;

namespace ResourcePulse.Domain.Tests.Projects;

public class ProjectNodeSkillRequirementTests
{
    [Fact]
    public void AddSkillRequirement_OnRoot_OK()
    {
        var p = Project();
        var skillId = Guid.NewGuid();

        p.AddSkillRequirement(skillId, SkillLevel.Proficient);

        p.SkillRequirements.Should().ContainSingle()
            .Which.Should().Match<ProjectSkillRequirement>(r =>
                r.SkillId == skillId && r.MinLevel == SkillLevel.Proficient);
    }

    [Fact]
    public void AddSkillRequirement_Duplicate_Throws()
    {
        var p = Project();
        var skillId = Guid.NewGuid();
        p.AddSkillRequirement(skillId, SkillLevel.Basic);

        var act = () => p.AddSkillRequirement(skillId, SkillLevel.Expert);

        act.Should().Throw<DomainException>().WithMessage("*already exists*");
    }

    [Fact]
    public void UpdateSkillRequirementLevel_ChangesLevel()
    {
        var p = Project();
        var skillId = Guid.NewGuid();
        p.AddSkillRequirement(skillId, SkillLevel.Basic);

        p.UpdateSkillRequirementLevel(skillId, SkillLevel.Expert);

        p.SkillRequirements.Single().MinLevel.Should().Be(SkillLevel.Expert);
    }

    [Fact]
    public void UpdateSkillRequirementLevel_Missing_Throws()
    {
        var p = Project();

        var act = () => p.UpdateSkillRequirementLevel(Guid.NewGuid(), SkillLevel.Expert);

        act.Should().Throw<DomainException>().WithMessage("*No skill requirement exists*");
    }

    [Fact]
    public void RemoveSkillRequirement_RemovesEntry()
    {
        var p = Project();
        var skillId = Guid.NewGuid();
        p.AddSkillRequirement(skillId, SkillLevel.Basic);

        p.RemoveSkillRequirement(skillId);

        p.SkillRequirements.Should().BeEmpty();
    }

    [Fact]
    public void RemoveSkillRequirement_Missing_Throws()
    {
        var p = Project();

        var act = () => p.RemoveSkillRequirement(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*No skill requirement exists*");
    }

    [Fact]
    public void AddSkillRequirement_OnPhase_Throws()
    {
        var p = Project();
        var phase = Phase(p);

        var act = () => phase.AddSkillRequirement(Guid.NewGuid(), SkillLevel.Basic);

        act.Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
    }

    [Fact]
    public void AddSkillRequirement_OnWorkPackage_Throws()
    {
        var p = Project();
        var wp = WorkPackage(p);

        var act = () => wp.AddSkillRequirement(Guid.NewGuid(), SkillLevel.Basic);

        act.Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
    }

    [Fact]
    public void UpdateSkillRequirement_OnNonRoot_Throws()
    {
        var p = Project();
        var phase = Phase(p);

        var act = () => phase.UpdateSkillRequirementLevel(Guid.NewGuid(), SkillLevel.Expert);

        act.Should().Throw<DomainException>().WithMessage("*only allowed on Project root nodes*");
    }
}
