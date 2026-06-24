using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// I6 (hard-commit threshold) is now EXTRACTED to CommitmentPolicy (ADR-0020): the
// rule is unchanged, but the threshold is read from config, not cabled. These
// tests prove the gate follows the configured set and NOT the old {Committed,
// Critical} literal.
public class HardCommitmentPolicyTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);

    private CreateCommand HardCreate(PlanCommandHarness h) => new()
    {
        ResourceId = h.ResourceId,
        ProjectNodeId = h.ProjectNodeId,
        PeriodStart = D1,
        PeriodEnd = D14,
        Percent = 40m,
        Status = AllocationStatus.Hard
    };

    [Fact]
    public async Task DefaultPolicy_PermitsHardOnCommittedProject()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Committed);

        var result = await h.Service.ExecuteAsync(HardCreate(h));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultPolicy_RejectsHardOnPlannedProject()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Planned);

        var result = await h.Service.ExecuteAsync(HardCreate(h));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Hard");
    }

    [Fact]
    public async Task WideningPolicyToIncludePlanned_NowPermitsHardOnPlannedProject()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Planned);

        // Before: Planned is not hard-committed under the default policy.
        (await h.Service.ExecuteAsync(HardCreate(h))).IsFailure.Should().BeTrue();

        // Reconfigure the threshold to include Planned.
        await h.CommitmentPolicy.UpdateAsync(new UpdateCommitmentPolicyDto
        {
            HardCommitLevels = [CommitmentLevel.Planned, CommitmentLevel.Committed, CommitmentLevel.Critical]
        });

        // After: the SAME command now succeeds — the gate read the new config.
        (await h.Service.ExecuteAsync(HardCreate(h))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task NarrowingPolicyToExcludeCommitted_RejectsHardThatTheOldLiteralWouldHaveAllowed()
    {
        var h = PlanCommandHarness.Create(CommitmentLevel.Committed);

        // The retired cabled value was {Committed, Critical}; reconfigure to drop
        // Committed so we can prove the gate no longer reads that literal.
        await h.CommitmentPolicy.UpdateAsync(new UpdateCommitmentPolicyDto
        {
            HardCommitLevels = [CommitmentLevel.Critical]
        });

        var result = await h.Service.ExecuteAsync(HardCreate(h));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Critical");
    }
}
