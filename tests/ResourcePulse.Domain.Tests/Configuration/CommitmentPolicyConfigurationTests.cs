using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Tests.Configuration;

public class CommitmentPolicyConfigurationTests
{
    [Fact]
    public void Default_IsCommittedAndCritical()
    {
        var policy = CommitmentPolicyConfiguration.CreateDefault();

        policy.HardCommitLevels.Should().BeEquivalentTo(
            new[] { CommitmentLevel.Committed, CommitmentLevel.Critical });
    }

    [Fact]
    public void IsHardCommitted_MatchesTheConfiguredSet()
    {
        var policy = CommitmentPolicyConfiguration.CreateDefault();

        policy.IsHardCommitted(CommitmentLevel.Committed).Should().BeTrue();
        policy.IsHardCommitted(CommitmentLevel.Critical).Should().BeTrue();
        policy.IsHardCommitted(CommitmentLevel.Planned).Should().BeFalse();
        policy.IsHardCommitted(CommitmentLevel.Exploratory).Should().BeFalse();
        policy.IsHardCommitted(null).Should().BeFalse();
    }

    [Fact]
    public void Replace_ChangesWhichLevelsCountAsHard()
    {
        var policy = CommitmentPolicyConfiguration.CreateDefault();
        policy.Replace([CommitmentLevel.Planned]);

        policy.IsHardCommitted(CommitmentLevel.Planned).Should().BeTrue();
        policy.IsHardCommitted(CommitmentLevel.Committed).Should().BeFalse();
    }

    [Fact]
    public void Create_DeduplicatesLevels()
    {
        var policy = CommitmentPolicyConfiguration.Create(Guid.NewGuid(),
            [CommitmentLevel.Committed, CommitmentLevel.Committed, CommitmentLevel.Critical]);

        policy.HardCommitLevels.Should().HaveCount(2);
    }

    [Fact]
    public void Create_RejectsEmptySet()
    {
        var act = () => CommitmentPolicyConfiguration.Create(Guid.NewGuid(), []);
        act.Should().Throw<DomainException>().WithMessage("*at least one hard-commit level*");
    }
}
