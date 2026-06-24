using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Tests.Configuration;

public class LoadBandConfigurationTests
{
    private static LoadBandConfiguration Default() => LoadBandConfiguration.CreateDefault();

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Default_HasFourBandsStartingAtZero()
    {
        var config = Default();

        config.Bands.Select(b => b.Label).Should().Equal("Under", "Healthy", "Full", "Overloaded");
        config.Bands.Select(b => b.LowerBound).Should().Equal(0m, 85m, 100m, 110m);
    }

    [Fact]
    public void Create_RejectsEmptyBandList()
    {
        var act = () => LoadBandConfiguration.Create(Guid.NewGuid(), []);
        act.Should().Throw<DomainException>().WithMessage("*at least one band*");
    }

    [Fact]
    public void Create_RejectsFirstBandNotStartingAtZero()
    {
        var act = () => LoadBandConfiguration.Create(Guid.NewGuid(),
            [("Low", 10m), ("High", 100m)]);
        act.Should().Throw<DomainException>().WithMessage("*must start at lower bound 0*");
    }

    [Fact]
    public void Create_RejectsNonStrictlyIncreasingBounds()
    {
        var act = () => LoadBandConfiguration.Create(Guid.NewGuid(),
            [("A", 0m), ("B", 85m), ("C", 85m)]); // duplicate bound = gap/overlap
        act.Should().Throw<DomainException>().WithMessage("*strictly increasing*");
    }

    [Fact]
    public void Create_RejectsDecreasingBounds()
    {
        var act = () => LoadBandConfiguration.Create(Guid.NewGuid(),
            [("A", 0m), ("B", 100m), ("C", 90m)]);
        act.Should().Throw<DomainException>().WithMessage("*strictly increasing*");
    }

    // ── Resolution: half-open [lower, nextLower) at the boundaries ─────────────

    [Theory]
    [InlineData(0, "Under")]
    [InlineData(84.9, "Under")]
    [InlineData(85, "Healthy")]
    [InlineData(99.99, "Healthy")]
    [InlineData(100, "Full")]
    [InlineData(110, "Overloaded")]
    [InlineData(110.1, "Overloaded")]
    [InlineData(500, "Overloaded")]
    public void Resolve_PicksBandByHalfOpenLowerBound(decimal loadPercent, string expectedLabel)
    {
        Default().Resolve(loadPercent).Label.Should().Be(expectedLabel);
    }

    // ── Different band counts both work ────────────────────────────────────────

    [Fact]
    public void Resolve_WorksWithThreeBandConfiguration()
    {
        var config = LoadBandConfiguration.Create(Guid.NewGuid(),
            [("Low", 0m), ("Mid", 50m), ("High", 100m)]);

        config.Bands.Should().HaveCount(3);
        config.Resolve(0m).Label.Should().Be("Low");
        config.Resolve(49.9m).Label.Should().Be("Low");
        config.Resolve(50m).Label.Should().Be("Mid");
        config.Resolve(100m).Label.Should().Be("High");
        config.Resolve(999m).Label.Should().Be("High");
    }

    [Fact]
    public void Resolve_WorksWithFiveBandConfiguration()
    {
        var config = LoadBandConfiguration.Create(Guid.NewGuid(),
            [("Idle", 0m), ("Light", 40m), ("Healthy", 70m), ("Full", 100m), ("Over", 120m)]);

        config.Bands.Should().HaveCount(5);
        config.Resolve(0m).Label.Should().Be("Idle");
        config.Resolve(69.9m).Label.Should().Be("Light");
        config.Resolve(70m).Label.Should().Be("Healthy");
        config.Resolve(119.9m).Label.Should().Be("Full");
        config.Resolve(120m).Label.Should().Be("Over");
    }

    [Fact]
    public void Replace_SwapsTheEntireLadder()
    {
        var config = Default();
        config.Replace([("A", 0m), ("B", 90m)]);

        config.Bands.Select(b => b.Label).Should().Equal("A", "B");
        config.Resolve(95m).Label.Should().Be("B");
    }
}
