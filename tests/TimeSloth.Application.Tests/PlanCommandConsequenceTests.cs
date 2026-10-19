using Microsoft.EntityFrameworkCore;
using TimeSloth.Domain.Allocations;
using TimeSloth.Services.Plan;

namespace TimeSloth.Application.Tests;

// The consequence of a node move (ADR-0036): what replanNode does beyond the
// dates. The harness capacity is a flat 8 h on every day, so every figure below
// is % x days x 8 — and "today" is pinned to 22 Jun 2026, which puts the frozen
// zone at 22 Jun – 6 Jul under the default fence.
public class PlanCommandConsequenceTests
{
    private static readonly DateOnly PhaseStart = new(2026, 7, 1);
    private static readonly DateOnly PhaseEnd = new(2026, 8, 30);
    private static readonly DateOnly Longer = new(2026, 9, 9);   // +10 days
    private static readonly DateOnly Shorter = new(2026, 8, 16); // -14 days

    private static async Task MakeHardAsync(PlanCommandHarness h, Guid id)
    {
        var r = await h.Service.ExecuteAsync(new ChangeStatusCommand { Id = id, Status = AllocationStatus.Hard });
        r.IsSuccess.Should().BeTrue(r.Error?.Message);
    }

    private static Task<Common.Results.ServiceResult<PlanCommandResult>> ReplanEndAsync(
        PlanCommandHarness h, Guid phase, DateOnly end, bool dryRun = true) =>
        h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            DryRun = dryRun, NodeId = phase, PlannedStart = PhaseStart, PlannedEnd = end
        });

    // 61 days x 60% x 8 h = 292.8 h covered at rest.
    private static async Task<(PlanCommandHarness h, Guid phase, Guid demand, Guid block)> SeedAsync(
        double requiredHours = 320, bool hard = true)
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd, "Fase 2");
        var demand = h.SeedDemandOn(phase, TimeSpan.FromHours(requiredHours));
        var block = h.SeedFollowingCoverage(demand, 60m);
        if (hard) await MakeHardAsync(h, block);
        return (h, phase, demand, block);
    }

    [Fact]
    public async Task ReplanNode_PricesTheHoursNobodyDecided()
    {
        var (h, phase, _, block) = await SeedAsync();

        var result = await ReplanEndAsync(h, phase, Longer);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var consequence = result.Value.Consequence!;
        consequence.Blocks.Should().ContainSingle().Which.Should().Match<PlanBlockShift>(b =>
            b.Id == block && b.OldEnd == PhaseEnd && b.NewEnd == Longer && b.HoursDelta == TimeSpan.FromHours(48));
        consequence.People.Should().ContainSingle().Which.HoursDelta.Should().Be(TimeSpan.FromHours(48));
    }

    [Fact]
    public async Task ReplanNode_LengtheningClosesAGap_AndSaysSoAsAStateChange()
    {
        // 320 h required, 292.8 h covered: 27.2 h uncovered. Ten more days add
        // 48 h and the demand reads covered without anyone staffing anyone.
        var (h, phase, demand, _) = await SeedAsync(requiredHours: 320);

        var result = await ReplanEndAsync(h, phase, Longer);

        result.Value.Consequence!.Demands.Should().ContainSingle().Which.Should().Match<PlanDemandConsequence>(d =>
            d.DemandId == demand
            && d.GapBefore == TimeSpan.FromHours(27.2)
            && d.GapAfter == TimeSpan.FromHours(-20.8)
            && d.StateChanged);
    }

    [Fact]
    public async Task ReplanNode_AGapThatShrinksAndStaysAGap_IsNotAStateChange()
    {
        var (h, phase, _, _) = await SeedAsync(requiredHours: 400);

        var result = await ReplanEndAsync(h, phase, Longer);

        var d = result.Value.Consequence!.Demands.Single();
        d.GapBefore.Should().Be(TimeSpan.FromHours(107.2));
        d.GapAfter.Should().Be(TimeSpan.FromHours(59.2));
        d.StateChanged.Should().BeFalse();
    }

    [Fact]
    public async Task ReplanNode_ShorteningReopensACoveredDemand()
    {
        // 280 h required, 292.8 h covered. Fourteen days fewer take 67.2 h away.
        var (h, phase, _, _) = await SeedAsync(requiredHours: 280);

        var result = await ReplanEndAsync(h, phase, Shorter);

        var consequence = result.Value.Consequence!;
        consequence.People.Single().HoursDelta.Should().Be(TimeSpan.FromHours(-67.2));
        consequence.Demands.Single().Should().Match<PlanDemandConsequence>(d =>
            d.GapBefore == TimeSpan.FromHours(-12.8) && d.GapAfter == TimeSpan.FromHours(54.4) && d.StateChanged);
    }

    [Fact]
    public async Task ReplanNode_ABestEffortDemand_HasNoGap_AndNeverChangesState()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        h.SeedFollowingCoverage(h.SeedDemandOn(phase, requiredHours: null), 60m);

        var result = await ReplanEndAsync(h, phase, Longer);

        result.Value.Consequence!.Demands.Single().Should().Match<PlanDemandConsequence>(d =>
            d.GapBefore == null && d.GapAfter == null && !d.StateChanged);
    }

    [Fact]
    public async Task ReplanNode_APersonChangesBand_CountingTheProjectThatIsNotOnScreen()
    {
        // 60% here + 50% on another project over the added days: 50% -> 110%,
        // which is the last band under the default configuration.
        var (h, phase, _, _) = await SeedAsync();
        var other = h.SeedRoot("Intranet Regione");
        var elsewhere = h.SeedCoverage(h.SeedDemandOn(other), new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30), 50m);
        await MakeHardAsync(h, elsewhere);

        var result = await ReplanEndAsync(h, phase, Longer);

        var interval = result.Value.Consequence!.People.Single().Intervals.Should().ContainSingle().Subject;
        interval.From.Should().Be(new DateOnly(2026, 8, 31));
        interval.To.Should().Be(Longer);
        interval.PercentBefore.Should().Be(50m);
        interval.PercentAfter.Should().Be(110m);
        interval.BandChanged.Should().BeTrue();
        interval.OverloadedAfter.Should().BeTrue();
        interval.OtherProjects.Should().ContainSingle().Which.Name.Should().Be("Intranet Regione");
    }

    [Fact]
    public async Task ReplanNode_ARateThatMovesInsideOneBand_IsReported_ButIsNotABandChange()
    {
        // 0% -> 60%: both under the second band's floor (85). The hours are in
        // the account; nobody changed state.
        var (h, phase, _, _) = await SeedAsync();

        var result = await ReplanEndAsync(h, phase, Longer);

        result.Value.Consequence!.People.Single().Intervals.Should().ContainSingle().Which.Should()
            .Match<PlanLoadInterval>(i => i.PercentBefore == 0m && i.PercentAfter == 60m && !i.BandChanged && !i.OverloadedAfter);
    }

    [Fact]
    public async Task ReplanNode_ATentativeBlock_AddsHours_ButMovesNoBand()
    {
        // The band counts Hard only, as the board and the detector do: a
        // proposal that stretches still resolves hours onto its demand.
        var (h, phase, _, _) = await SeedAsync(hard: false);

        var result = await ReplanEndAsync(h, phase, Longer);

        var person = result.Value.Consequence!.People.Single();
        person.HoursDelta.Should().Be(TimeSpan.FromHours(48));
        person.Intervals.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplanNode_HoursPastTheDeclaredAvailability_AreCounted_AndNamed()
    {
        var (h, phase, _, _) = await SeedAsync();
        h.SetResourceAvailability(h.ResourceId, null, new DateOnly(2026, 8, 31));

        var result = await ReplanEndAsync(h, phase, Longer);

        var person = result.Value.Consequence!.People.Single();
        person.HoursDelta.Should().Be(TimeSpan.FromHours(48));
        person.BeyondAvailability.Should().Match<PlanBeyondAvailability>(b =>
            b.From == new DateOnly(2026, 9, 1) && b.To == Longer && b.Days == 9 && b.Hours == TimeSpan.FromHours(43.2));
    }

    [Fact]
    public async Task ReplanNode_TouchesTheFrozenZone_WhenAChangedDayFallsInIt()
    {
        // Frozen is 22 Jun – 6 Jul. Pulling the start of the phase back to
        // 29 Jun adds two days inside it.
        var (h, phase, _, _) = await SeedAsync();

        var inside = await h.Service.ExecuteAsync(new ReplanNodeCommand
        {
            DryRun = true, NodeId = phase, PlannedStart = new DateOnly(2026, 6, 29), PlannedEnd = PhaseEnd
        });
        var outside = await ReplanEndAsync(h, phase, Longer);

        inside.Value.Consequence!.TouchesFrozen.Should().BeTrue();
        inside.Value.Consequence.FrozenUntil.Should().Be(new DateOnly(2026, 7, 6));
        outside.Value.Consequence!.TouchesFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task ReplanNode_WithNothingFollowing_StillAnswers_WithAnEmptyAccount()
    {
        var h = PlanCommandHarness.Create();
        var phase = h.SeedPhase(h.ProjectNodeId, PhaseStart, PhaseEnd);
        h.SeedCoverage(h.SeedDemandOn(phase), PhaseStart, PhaseEnd);

        var result = await ReplanEndAsync(h, phase, Longer);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Changes.Should().BeEmpty();
        result.Value.Consequence.Should().NotBeNull();
        result.Value.Consequence!.People.Should().BeEmpty();
        result.Value.Consequence.Demands.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplanNode_TheEstimateIsTheReceipt()
    {
        // Same command, dryRun then commit: the two accounts are the same
        // account, because they are the same code reading the same "before".
        var (h, phase, _, block) = await SeedAsync();

        var estimate = await ReplanEndAsync(h, phase, Longer, dryRun: true);
        var receipt = await ReplanEndAsync(h, phase, Longer, dryRun: false);

        h.Reload(block).PeriodEnd.Should().Be(Longer);
        receipt.Value.Committed.Should().BeTrue();
        receipt.Value.Consequence.Should().BeEquivalentTo(estimate.Value.Consequence);
    }

    [Fact]
    public async Task ReplanNode_DryRun_SeedsNoConfigurationBehindTheCallersBack()
    {
        // The band and fence reads are get-or-seed. They run before the kernel
        // mutates anything; were they to run after, the seed's SaveChanges would
        // commit the dry run.
        var (h, phase, _, block) = await SeedAsync();

        await ReplanEndAsync(h, phase, Longer, dryRun: true);

        h.Reload(block).PeriodEnd.Should().Be(PhaseEnd);
        h.Db.ProjectNodes.AsNoTracking().Single(p => p.Id == phase).PlannedEnd.Should().Be(PhaseEnd);
    }

    [Fact]
    public async Task MoveSubtree_IsPricedToo()
    {
        var (h, _, _, _) = await SeedAsync();

        var result = await h.Service.ExecuteAsync(new MoveSubtreeCommand
        {
            DryRun = true, NodeId = h.ProjectNodeId, DeltaDays = 7
        });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        // A translation gives back as many days as it takes: no hours on a flat
        // calendar, and the account still lists the block that moved.
        result.Value.Consequence!.Blocks.Should().ContainSingle();
        result.Value.Consequence.People.Single().HoursDelta.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task KindsThatMoveOnlyWhatTheUserTouched_CarryNoConsequence()
    {
        var (h, _, _, block) = await SeedAsync();

        var result = await h.Service.ExecuteAsync(new PinCommand { DryRun = true, Id = block, Edge = BoundaryEdge.End });

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Consequence.Should().BeNull();
    }
}
