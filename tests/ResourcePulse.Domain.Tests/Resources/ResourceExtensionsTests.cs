using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Resources;

public class ResourceExtensionsTests
{
    private static Resource MakeResource() =>
        Resource.Create("Alice", Guid.NewGuid());

    // ── Team ────────────────────────────────────────────────────────────────

    [Fact]
    public void AssignToTeam_SetsTeamId_AndRaisesEvent()
    {
        var resource = MakeResource();
        resource.ClearDomainEvents();
        var teamId = Guid.NewGuid();

        resource.AssignToTeam(teamId);

        resource.TeamId.Should().Be(teamId);
        resource.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ResourceTeamChanged>()
            .Which.Should().Match<ResourceTeamChanged>(e =>
                e.ResourceId == resource.Id &&
                e.OldTeamId == null &&
                e.NewTeamId == teamId);
    }

    [Fact]
    public void AssignToTeam_NoChange_DoesNotRaiseEvent()
    {
        var resource = MakeResource();
        var teamId = Guid.NewGuid();
        resource.AssignToTeam(teamId);
        resource.ClearDomainEvents();

        resource.AssignToTeam(teamId);

        resource.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignToTeam_Null_ClearsAssignment_AndRaisesEvent()
    {
        var resource = MakeResource();
        var teamId = Guid.NewGuid();
        resource.AssignToTeam(teamId);
        resource.ClearDomainEvents();

        resource.AssignToTeam(null);

        resource.TeamId.Should().BeNull();
        resource.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ResourceTeamChanged>()
            .Which.OldTeamId.Should().Be(teamId);
    }

    [Fact]
    public void AssignToTeam_EmptyGuid_TreatedAsNull()
    {
        var resource = MakeResource();
        var teamId = Guid.NewGuid();
        resource.AssignToTeam(teamId);
        resource.ClearDomainEvents();

        resource.AssignToTeam(Guid.Empty);

        resource.TeamId.Should().BeNull();
    }

    // ── Skills ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddSkill_AddsToCollection()
    {
        var resource = MakeResource();
        var skillId = Guid.NewGuid();

        resource.AddSkill(skillId, SkillLevel.Proficient);

        resource.Skills.Should().ContainSingle()
            .Which.Should().Match<ResourceSkill>(s => s.SkillId == skillId && s.Level == SkillLevel.Proficient);
    }

    [Fact]
    public void AddSkill_Duplicate_Throws()
    {
        var resource = MakeResource();
        var skillId = Guid.NewGuid();
        resource.AddSkill(skillId, SkillLevel.Basic);

        var act = () => resource.AddSkill(skillId, SkillLevel.Expert);

        act.Should().Throw<DomainException>()
            .WithMessage("*already has skill*");
    }

    [Fact]
    public void UpdateSkillLevel_ChangesLevel()
    {
        var resource = MakeResource();
        var skillId = Guid.NewGuid();
        resource.AddSkill(skillId, SkillLevel.Basic);

        resource.UpdateSkillLevel(skillId, SkillLevel.Expert);

        resource.Skills.Single().Level.Should().Be(SkillLevel.Expert);
    }

    [Fact]
    public void UpdateSkillLevel_NotPresent_Throws()
    {
        var resource = MakeResource();

        var act = () => resource.UpdateSkillLevel(Guid.NewGuid(), SkillLevel.Expert);

        act.Should().Throw<DomainException>()
            .WithMessage("*does not have skill*");
    }

    [Fact]
    public void RemoveSkill_RemovesFromCollection()
    {
        var resource = MakeResource();
        var skillId = Guid.NewGuid();
        resource.AddSkill(skillId, SkillLevel.Basic);

        resource.RemoveSkill(skillId);

        resource.Skills.Should().BeEmpty();
    }

    [Fact]
    public void RemoveSkill_NotPresent_Throws()
    {
        var resource = MakeResource();

        var act = () => resource.RemoveSkill(Guid.NewGuid());

        act.Should().Throw<DomainException>()
            .WithMessage("*does not have skill*");
    }

    // ── Tags ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTag_AddsToCollection()
    {
        var resource = MakeResource();
        var tagId = Guid.NewGuid();

        resource.AddTag(tagId);

        resource.Tags.Should().ContainSingle()
            .Which.TagId.Should().Be(tagId);
    }

    [Fact]
    public void AddTag_Duplicate_Throws()
    {
        var resource = MakeResource();
        var tagId = Guid.NewGuid();
        resource.AddTag(tagId);

        var act = () => resource.AddTag(tagId);

        act.Should().Throw<DomainException>()
            .WithMessage("*already has tag*");
    }

    [Fact]
    public void RemoveTag_RemovesFromCollection()
    {
        var resource = MakeResource();
        var tagId = Guid.NewGuid();
        resource.AddTag(tagId);

        resource.RemoveTag(tagId);

        resource.Tags.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_NotPresent_Throws()
    {
        var resource = MakeResource();

        var act = () => resource.RemoveTag(Guid.NewGuid());

        act.Should().Throw<DomainException>()
            .WithMessage("*does not have tag*");
    }
}
