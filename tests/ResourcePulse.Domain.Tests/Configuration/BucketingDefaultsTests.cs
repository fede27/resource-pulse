using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Tests.Configuration;

public class BucketingDefaultsTests
{
    [Fact]
    public void Default_IsWeekThenMonth()
    {
        var config = BucketingDefaults.CreateDefault();

        config.PrimaryGrain.Should().Be(BucketGrain.Week);
        config.SecondaryGrain.Should().Be(BucketGrain.Month);
    }

    [Fact]
    public void Replace_UpdatesBothGrains()
    {
        var config = BucketingDefaults.CreateDefault();
        config.Replace(BucketGrain.Day, BucketGrain.Week);

        config.PrimaryGrain.Should().Be(BucketGrain.Day);
        config.SecondaryGrain.Should().Be(BucketGrain.Week);
    }

    [Fact]
    public void Create_RejectsValueOutsideEnum()
    {
        var act = () => BucketingDefaults.Create(Guid.NewGuid(), (BucketGrain)99, BucketGrain.Month);
        act.Should().Throw<DomainException>().WithMessage("*Invalid primary bucket grain*");
    }

    [Fact]
    public void Create_RejectsSecondaryValueOutsideEnum()
    {
        var act = () => BucketingDefaults.Create(Guid.NewGuid(), BucketGrain.Week, (BucketGrain)0);
        act.Should().Throw<DomainException>().WithMessage("*Invalid secondary bucket grain*");
    }
}
