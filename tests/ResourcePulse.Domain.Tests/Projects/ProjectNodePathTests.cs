using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Domain.Tests.Projects;

// Reading the materialized path (ADR-0001). It used to be open-coded in five
// places; now that there is one implementation it is worth pinning what it does
// with the edges, because every caller inherits the answer.
public class ProjectNodePathTests
{
    private static readonly Guid Root = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Child = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void TheRootIsTheFirstSegment()
    {
        ProjectNodePath.RootId($"/{Root}").Should().Be(Root);
        ProjectNodePath.RootId($"/{Root}/{Child}").Should().Be(Root);
        ProjectNodePath.RootId($"/{Root}/{Child}/{Guid.NewGuid()}").Should().Be(Root);
    }

    [Fact]
    public void ALeadingSeparatorIsOptional()
    {
        // The three copies this replaced all trimmed it, so keep accepting both.
        ProjectNodePath.RootId($"{Root}/{Child}").Should().Be(Root);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("not-a-guid")]
    [InlineData("/not-a-guid/22222222-2222-2222-2222-222222222222")]
    public void AnUnreadablePathIsRefused(string path)
    {
        ProjectNodePath.TryGetRootId(path, out var rootId).Should().BeFalse();
        rootId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ANullPathIsRefusedRatherThanThrowing()
    {
        ProjectNodePath.TryGetRootId(null, out _).Should().BeFalse();
    }

    [Fact]
    public void TheThrowingOverloadDoesNotThrowADomainException()
    {
        // Services translate DomainException into a 409. A path that cannot be read
        // is corrupt data, not something the caller can resolve by trying again.
        var act = () => ProjectNodePath.RootId("/nonsense");

        act.Should().Throw<ArgumentException>();
        act.Should().NotThrow<DomainException>();
    }
}
