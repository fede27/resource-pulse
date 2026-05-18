namespace ResourcePulse.Domain.Tests.Factories;

public class CatalogInvariantTests
{
    // ── Team ────────────────────────────────────────────────────────────────

    [Fact]
    public void Team_Create_EmptyName_Throws()
    {
        var act = () => Team.Create("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void Team_Create_TrimsName()
    {
        var team = Team.Create("  Engineering  ");

        team.Name.Should().Be("Engineering");
    }

    [Fact]
    public void Team_Create_DefaultsToActive()
    {
        var team = Team.Create("Engineering");

        team.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Team_Rename_EmptyName_Throws()
    {
        var team = Team.Create("Engineering");

        var act = () => team.Rename("");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void Team_DeactivateAndActivate_TogglesFlag()
    {
        var team = Team.Create("Engineering");

        team.Deactivate();
        team.IsActive.Should().BeFalse();

        team.Activate();
        team.IsActive.Should().BeTrue();
    }

    // ── Skill ───────────────────────────────────────────────────────────────

    [Fact]
    public void Skill_Create_EmptyName_Throws()
    {
        var act = () => Skill.Create("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void Skill_Create_TrimsName()
    {
        var skill = Skill.Create("  C#  ");

        skill.Name.Should().Be("C#");
    }

    [Fact]
    public void Skill_Create_NoCategory_StoresNull()
    {
        var skill = Skill.Create("C#");

        skill.Category.Should().BeNull();
    }

    [Fact]
    public void Skill_Create_BlankCategory_StoresNull()
    {
        var skill = Skill.Create("C#", "   ");

        skill.Category.Should().BeNull();
    }

    [Fact]
    public void Skill_Create_TrimsCategory()
    {
        var skill = Skill.Create("C#", "  Languages  ");

        skill.Category.Should().Be("Languages");
    }

    [Fact]
    public void Skill_ChangeCategory_NullClears()
    {
        var skill = Skill.Create("C#", "Languages");

        skill.ChangeCategory(null);

        skill.Category.Should().BeNull();
    }

    [Fact]
    public void Skill_Rename_EmptyName_Throws()
    {
        var skill = Skill.Create("C#");

        var act = () => skill.Rename("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    // ── Tag ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tag_Create_EmptyName_Throws()
    {
        var act = () => Tag.Create("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }

    [Fact]
    public void Tag_Create_NormalizesToLowerAndTrim()
    {
        var tag = Tag.Create("  Strategic  ");

        tag.Name.Should().Be("strategic");
    }

    [Fact]
    public void Tag_Create_MixedCase_NormalizesConsistently()
    {
        var a = Tag.Create("STRATEGIC");
        var b = Tag.Create("strategic");

        a.Name.Should().Be(b.Name);
    }

    [Fact]
    public void Tag_Rename_NormalizesNewName()
    {
        var tag = Tag.Create("strategic");

        tag.Rename("  HighPriority  ");

        tag.Name.Should().Be("highpriority");
    }

    [Fact]
    public void Tag_Rename_EmptyName_Throws()
    {
        var tag = Tag.Create("strategic");

        var act = () => tag.Rename("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("*name must not be empty*");
    }
}
