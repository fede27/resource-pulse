using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.Application.Tests;

// The detector (ADR-0032 §9 / ADR-0033). What these tests pin is the RULES —
// admission, reconciliation, aggregation, deadlines — not the SQL.
public class SignalDetectionTests
{
    private static readonly DateOnly Today = SignalDetectionHarness.Today;

    // ── Gap ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UncoveredDemand_ProducesAGapKeyedOnTheDemand()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();

        var gap = h.LiveOf(SignalKind.Gap, demandId);
        gap.Should().NotBeNull();
        gap!.Magnitude.Should().Be(40m);
        gap.Tier.Should().Be(SignalTier.Breach);
        gap.TouchedRootProjectIds.Should().Equal(h.ProjectId);
    }

    [Fact]
    public async Task BestEffortDemand_IsNeverAGap()
    {
        // No target ⇒ no defined gap (revision §7). Not a gap of zero: triaging it
        // would invent a target the plan deliberately does not have.
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(requiredHours: null);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap).Should().BeNull();
    }

    [Fact]
    public async Task CoverageDeliveredBeforeToday_CountsAgainstTheGap()
    {
        // THE range-scoping trap. Reconciling only over the committing horizon
        // would ignore coverage already delivered and light up every in-flight
        // project with a phantom gap — so the reconciliation window is the union
        // of the active roots' planned windows, not the horizon.
        var h = SignalDetectionHarness.Create();
        var past = h.SeedProject("Running", plannedStart: Today.AddDays(-60), plannedEnd: Today.AddDays(60));

        // 40h required; covered 100% for 5 days in the PAST at 8h/day = 40h.
        var demandId = h.SeedDemand(TimeSpan.FromHours(40), nodeId: past);
        h.SeedCoverage(demandId, Today.AddDays(-10), Today.AddDays(-6), percent: 100m);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, demandId).Should().BeNull();
    }

    [Fact]
    public async Task PartialCoverage_LeavesTheResidualAsTheMagnitude()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        // 5 days × 8h × 50% = 20h covered.
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, demandId)!.Magnitude.Should().Be(20m);
    }

    // ── Deadlines and admission (ADR-0033) ───────────────────────────────────

    [Fact]
    public async Task TheDerivedDeadline_IsThePlannedStartMinusTheLeadTime()
    {
        // Zero friction: nobody filled anything, and the demand still has a
        // deadline to rank on.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));   // project starts +30d

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, demandId)!.DeadlineAt
            .Should().Be(Today.AddDays(30).AddDays(-14));       // default lead: 2 weeks
    }

    [Fact]
    public async Task AnExplicitDecideBy_OverridesTheDerivedDeadline()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40), decideBy: Today.AddDays(3));

        await h.SweepAsync();

        var gap = h.LiveOf(SignalKind.Gap, demandId)!;
        gap.DeadlineAt.Should().Be(Today.AddDays(3));
        gap.Zone.Should().Be(SignalZone.Frozen);
    }

    [Fact]
    public async Task APastDeadline_IsOverdue_AndOutranksFrozen()
    {
        var h = SignalDetectionHarness.Create();
        var overdue = h.SeedDemand(TimeSpan.FromHours(10), decideBy: Today.AddDays(-5));
        var frozen = h.SeedDemand(TimeSpan.FromHours(400), decideBy: Today.AddDays(2));

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, overdue)!.Zone.Should().Be(SignalZone.Overdue);

        // And the ranking puts it first despite being forty times smaller.
        SignalRanking.Rank(h.Live()).First().SubjectId.Should().Be(overdue);
        h.LiveOf(SignalKind.Gap, frozen)!.Zone.Should().Be(SignalZone.Frozen);
    }

    [Fact]
    public async Task ADeadlineBeyondTheHorizon_IsNotAdmitted()
    {
        // A gap on a project starting in eight months is real, and it is not
        // triage: the queue is the committing horizon (ADR-0033 §8).
        var h = SignalDetectionHarness.Create();
        var far = h.SeedProject("Far", plannedStart: Today.AddDays(240), plannedEnd: Today.AddDays(400));
        var demandId = h.SeedDemand(TimeSpan.FromHours(40), nodeId: far);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, demandId).Should().BeNull();
    }

    [Fact]
    public async Task ADemandOnAnUndatedPhase_FallsBackToTheRootsPlannedStart()
    {
        // ADR-0033 §3: not a parent-child date invariant — the resolution of a
        // single read. Without it, an undated phase on a perfectly dated project
        // would surface as model hygiene, which is noise.
        var h = SignalDetectionHarness.Create();
        var phase = h.SeedPhase("Fase 1", h.ProjectId);
        var demandId = h.SeedDemand(TimeSpan.FromHours(40), nodeId: phase);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, demandId)!.DeadlineAt.Should().Be(Today.AddDays(30).AddDays(-14));
        h.LiveOf(SignalKind.DemandOnUndatedNode).Should().BeNull();
    }

    [Fact]
    public async Task HardCommitmentIsCarried_ForTheRanking()
    {
        var h = SignalDetectionHarness.Create(CommitmentLevel.Committed);
        var proposed = h.SeedProject("Proposta", CommitmentLevel.Exploratory,
            Today.AddDays(30), Today.AddDays(120));

        var committedDemand = h.SeedDemand(TimeSpan.FromHours(40));
        var proposedDemand = h.SeedDemand(TimeSpan.FromHours(40), nodeId: proposed);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Gap, committedDemand)!.HardCommitted.Should().BeTrue();
        h.LiveOf(SignalKind.Gap, proposedDemand)!.HardCommitted.Should().BeFalse();
    }

    // ── Tentative in the frozen zone ─────────────────────────────────────────

    [Fact]
    public async Task ATentativeOverlappingTheFrozenZone_IsABreach()
    {
        // "Crossed the fence" means the window OVERLAPS the frozen zone, not that
        // it starts before the boundary.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        var coverageId = h.SeedCoverage(
            demandId, Today.AddDays(-3), Today.AddDays(20), percent: 50m, status: AllocationStatus.Tentative);

        await h.SweepAsync();

        var signal = h.LiveOf(SignalKind.TentativeInFrozen, coverageId);
        signal.Should().NotBeNull();
        signal!.Zone.Should().Be(SignalZone.Frozen);
        // 15 frozen days (today → +14) × 8h × 50% = 60h not confirmed.
        signal.Magnitude.Should().Be(60m);
    }

    [Fact]
    public async Task ATentativeEntirelyBeyondTheFrozenZone_IsNotABreach()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(45),
            status: AllocationStatus.Tentative);

        await h.SweepAsync();

        h.LiveOf(SignalKind.TentativeInFrozen).Should().BeNull();
    }

    [Fact]
    public async Task AHardCoverageInTheFrozenZone_IsNotABreach()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        h.SeedCoverage(demandId, Today, Today.AddDays(10), status: AllocationStatus.Hard);

        await h.SweepAsync();

        h.LiveOf(SignalKind.TentativeInFrozen).Should().BeNull();
    }

    // ── Overcommit and slack ─────────────────────────────────────────────────

    [Fact]
    public async Task AResourceOverTheOverloadFloor_IsABreachKeyedOnThePerson()
    {
        // Keyed on the resource, not on (resource, week): a peak sliding by seven
        // days must read as "worsened", not "resolved + new".
        var h = SignalDetectionHarness.Create();
        var other = h.SeedProject("Beta", plannedStart: Today, plannedEnd: Today.AddDays(90));

        var d1 = h.SeedDemand(TimeSpan.FromHours(400));
        var d2 = h.SeedDemand(TimeSpan.FromHours(400), nodeId: other);
        h.SeedCoverage(d1, Today, Today.AddDays(30), percent: 80m);
        h.SeedCoverage(d2, Today, Today.AddDays(30), percent: 60m);   // 140% total

        await h.SweepAsync();

        var over = h.LiveOf(SignalKind.Overcommit, h.ResourceId);
        over.Should().NotBeNull();
        over!.Magnitude.Should().Be(30m);   // 140 − 110 (default overload floor)
        over.TouchedRootProjectIds.Should().BeEquivalentTo([h.ProjectId, other]);
    }

    [Fact]
    public async Task TentativeBlocksDoNotCountTowardsOvercommit()
    {
        // Hard-only, like the sustainability verdict: a tentative is not a
        // commitment.
        var h = SignalDetectionHarness.Create();
        var other = h.SeedProject("Beta", plannedStart: Today, plannedEnd: Today.AddDays(90));

        var d1 = h.SeedDemand(TimeSpan.FromHours(400));
        var d2 = h.SeedDemand(TimeSpan.FromHours(400), nodeId: other);
        h.SeedCoverage(d1, Today, Today.AddDays(30), percent: 80m);
        h.SeedCoverage(d2, Today, Today.AddDays(30), percent: 60m, status: AllocationStatus.Tentative);

        await h.SweepAsync();

        h.LiveOf(SignalKind.Overcommit).Should().BeNull();
    }

    [Fact]
    public async Task UnderBand_IsOneAggregatedRow_NeverOnePerPerson()
    {
        // The most important constraint of the whole surface: no per-person row,
        // no magnitude derived from a utilization rate, no ordering between people.
        var h = SignalDetectionHarness.Create();
        var second = h.SeedResource("Caio");
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));

        h.SeedCoverage(demandId, Today, Today.AddDays(30), percent: 20m);
        h.SeedCoverage(demandId, Today, Today.AddDays(30), percent: 30m, resourceId: second);

        await h.SweepAsync();

        var slack = h.LiveOf(SignalKind.UnderBand);
        slack.Should().NotBeNull();
        slack!.SubjectId.Should().BeNull();
        slack.Shape.Should().Be(SignalShape.Aggregate);
        slack.Magnitude.Should().Be(2m);   // a cardinality
        slack.MemberSubjectIds.Should().BeEquivalentTo([h.ResourceId, second]);
        slack.DeadlineAt.Should().BeNull();
    }

    [Fact]
    public async Task AZeroPercentSegment_IsAbsenceOfData_NotUnderload()
    {
        // A person with no allocations at all is not "idle": the plan simply says
        // nothing about them. Counting it would turn a gap in planning into a
        // judgement about a person.
        var h = SignalDetectionHarness.Create();
        h.SeedResource("Nessuna allocazione");

        await h.SweepAsync();

        h.LiveOf(SignalKind.UnderBand).Should().BeNull();
    }

    // ── Hygiene ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DemandOnACancelledRoot_SurfacesAsHygiene()
    {
        // I4 forbids covering it and demands/coverage excludes those roots, so
        // without this kind the row exists and nothing can see it.
        var h = SignalDetectionHarness.Create();
        var dead = h.SeedProject("Omega", plannedStart: Today, plannedEnd: Today.AddDays(30), cancelled: true);
        h.SeedDemand(TimeSpan.FromHours(40), nodeId: dead);

        await h.SweepAsync();

        var hygiene = h.LiveOf(SignalKind.DemandOnClosedRoot);
        hygiene.Should().NotBeNull();
        hygiene!.Magnitude.Should().Be(1m);
        hygiene.Tier.Should().Be(SignalTier.Hygiene);
        hygiene.Zone.Should().BeNull();     // hygiene has no deadline
    }

    [Fact]
    public async Task HygieneIsAggregated_SoTwelveOccurrencesAreOneRow()
    {
        // The prototype emits one row per occurrence, which on a real tenant
        // floods a queue with a budget of seven — breaking the O(1) cardinality it
        // declares for itself.
        var h = SignalDetectionHarness.Create();
        for (var i = 0; i < 12; i++)
        {
            var dead = h.SeedProject($"Chiuso{i}", plannedStart: Today, plannedEnd: Today.AddDays(30), cancelled: true);
            h.SeedDemand(TimeSpan.FromHours(40), nodeId: dead);
        }

        await h.SweepAsync();

        h.Live().Count(s => s.Kind == SignalKind.DemandOnClosedRoot).Should().Be(1);
        h.LiveOf(SignalKind.DemandOnClosedRoot)!.Magnitude.Should().Be(12m);
    }

    [Fact]
    public async Task CoverageOutsideTheNodesPlannedWindow_SurfacesAsHygiene()
    {
        var h = SignalDetectionHarness.Create();   // project window: +30 → +180
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        h.SeedCoverage(demandId, Today.AddDays(150), Today.AddDays(220));  // overruns the end

        await h.SweepAsync();

        h.LiveOf(SignalKind.CoverageOutOfWindow)!.Magnitude.Should().Be(1m);
    }

    [Fact]
    public async Task ADeactivatedResourceWithFutureCoverage_SurfacesAsHygiene()
    {
        // I3 is evaluated at creation only, so deactivating someone afterwards
        // leaves coverage counting hours nobody will work.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(60));

        var resource = h.Db.Resources.Single(r => r.Id == h.ResourceId);
        resource.Deactivate();
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        await h.SweepAsync();

        h.LiveOf(SignalKind.InactiveWithCoverage)!.Magnitude.Should().Be(1m);
    }

    [Fact]
    public async Task ADemandOnANodeWithNoDatesAnywhere_SurfacesAsHygiene()
    {
        var h = SignalDetectionHarness.Create();
        var undated = h.SeedProject("Senza date");   // no planned dates at all
        h.SeedDemand(TimeSpan.FromHours(40), nodeId: undated);

        await h.SweepAsync();

        h.LiveOf(SignalKind.DemandOnUndatedNode)!.Magnitude.Should().Be(1m);
    }

    [Fact]
    public async Task NoDefaultCalendar_SurfacesAsHygiene()
    {
        var h = SignalDetectionHarness.Create();
        var calendar = h.Db.BusinessCalendars.Single(c => c.Id == h.CalendarId);
        calendar.UnmarkDefault();
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        await h.SweepAsync();

        h.LiveOf(SignalKind.NoDefaultCalendar).Should().NotBeNull();
    }

    // ── Reconciliation across passes ─────────────────────────────────────────

    [Fact]
    public async Task ASecondPassOverTheSameCondition_KeepsTheSameRow()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        var firstId = h.LiveOf(SignalKind.Gap, demandId)!.Id;
        var firstDetectedAt = h.Reload(firstId).FirstDetectedAt;

        var second = await h.SweepAsync();

        second.Value!.Created.Should().Be(0);
        h.LiveOf(SignalKind.Gap, demandId)!.Id.Should().Be(firstId);
        h.Reload(firstId).FirstDetectedAt.Should().Be(firstDetectedAt);
    }

    [Fact]
    public async Task AGrowingGap_Worsens_AndKeepsThePreviousMagnitude()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);  // 20h → gap 20h

        await h.SweepAsync();

        // The coverage is removed: the gap grows back to the full target.
        h.Db.Allocations.RemoveRange(h.Db.Allocations.ToList());
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        var result = await h.SweepAsync();

        result.Value!.Worsened.Should().Be(1);
        var gap = h.LiveOf(SignalKind.Gap, demandId)!;
        gap.Magnitude.Should().Be(40m);
        gap.PreviousMagnitude.Should().Be(20m);
        gap.LastChange.Should().HaveFlag(SignalChange.MagnitudeWorsened);
    }

    [Fact]
    public async Task AShrinkingGap_UpdatesTheDataButStaysOutOfTheFeed()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);

        var result = await h.SweepAsync();

        result.Value!.Worsened.Should().Be(0);
        h.LiveOf(SignalKind.Gap, demandId)!.Magnitude.Should().Be(20m);
    }

    [Fact]
    public async Task AConditionThatDisappears_IsResolvedByTheDetector()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        var signalId = h.LiveOf(SignalKind.Gap, demandId)!.Id;

        // Fully covered: 20 days × 8h × 100% ≫ 40h.
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(49), percent: 100m);
        var result = await h.SweepAsync();

        result.Value!.Resolved.Should().Be(1);
        h.Reload(signalId).Detection.Should().Be(SignalDetection.Resolved);
        h.LiveOf(SignalKind.Gap, demandId).Should().BeNull();
    }

    [Fact]
    public async Task AReturningCondition_GetsANewRow_SoTheAcknowledgementDoesNotSurvive()
    {
        // ADR-0032 §2 + decision 1: an acknowledgement dies with the resolution,
        // and it is STRUCTURAL — the acknowledgement was on the old row.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        var firstId = h.LiveOf(SignalKind.Gap, demandId)!.Id;

        var tracked = h.Db.PlanSignals.Single(s => s.Id == firstId);
        tracked.Acknowledge("elena", "va bene così", DateTimeOffset.UtcNow);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        // Cover it (resolves), then uncover it (returns).
        var coverageId = h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(49), percent: 100m);
        await h.SweepAsync();

        h.Db.Allocations.Remove(h.Db.Allocations.Single(a => a.Id == coverageId));
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();
        await h.SweepAsync();

        var current = h.LiveOf(SignalKind.Gap, demandId)!;
        current.Id.Should().NotBe(firstId);
        current.IsAcknowledged.Should().BeFalse();
        h.Reload(firstId).Detection.Should().Be(SignalDetection.Resolved);
    }

    [Fact]
    public async Task AnAcknowledgedSignal_StaysInTheQueue()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        var tracked = h.Db.PlanSignals.Single(s => s.Kind == SignalKind.Gap);
        tracked.Acknowledge("elena", "rischio assunto", DateTimeOffset.UtcNow);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        await h.SweepAsync();

        var signal = h.LiveOf(SignalKind.Gap, demandId);
        signal.Should().NotBeNull();
        signal!.IsAcknowledged.Should().BeTrue();
        signal.IsLive.Should().BeTrue();
    }

    // ── The resolve-only hook (ADR-0032 §9) ──────────────────────────────────

    [Fact]
    public async Task TheHookResolvesATouchedConditionThatIsGone()
    {
        // The UX it exists for: cover a gap and the row goes at once, instead of
        // lingering until tomorrow's sweep.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var signalId = h.LiveOf(SignalKind.Gap, demandId)!.Id;
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(49), percent: 100m);

        var resolved = await h.Detector.ResolveStaleAsync(
            new SignalTouch([demandId], [], []), Today);

        resolved.Value.Should().Be(1);
        h.Reload(signalId).Detection.Should().Be(SignalDetection.Resolved);
    }

    [Fact]
    public async Task TheHookNeverCreates()
    {
        // The asymmetry, for two independent reasons: it kills flapping at the
        // source, and the dashboard is meant to tell you about what you did NOT
        // just do.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        var resolved = await h.Detector.ResolveStaleAsync(
            new SignalTouch([demandId], [], []), Today);

        resolved.Value.Should().Be(0);
        h.Live().Should().BeEmpty();
    }

    [Fact]
    public async Task TheHookLeavesAConditionThatStillHolds()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        // Partial coverage: the gap shrinks but does not close.
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);

        var resolved = await h.Detector.ResolveStaleAsync(
            new SignalTouch([demandId], [], []), Today);

        resolved.Value.Should().Be(0);
        h.LiveOf(SignalKind.Gap, demandId).Should().NotBeNull();
    }

    [Fact]
    public async Task TheHookOnlyTouchesWhatTheCommandTouched()
    {
        // Two gaps, both closed; only one is named by the touch. The untouched one
        // must survive until the sweep — the hook is a targeted courtesy, not a
        // second scheduler.
        var h = SignalDetectionHarness.Create();
        var beta = h.SeedProject("Beta", plannedStart: Today.AddDays(30), plannedEnd: Today.AddDays(120));
        var mine = h.SeedDemand(TimeSpan.FromHours(40));
        var other = h.SeedDemand(TimeSpan.FromHours(40), nodeId: beta);
        await h.SweepAsync();

        h.SeedCoverage(mine, Today.AddDays(30), Today.AddDays(49), percent: 100m);
        h.SeedCoverage(other, Today.AddDays(30), Today.AddDays(49), percent: 100m);

        await h.Detector.ResolveStaleAsync(new SignalTouch([mine], [], []), Today);

        h.LiveOf(SignalKind.Gap, mine).Should().BeNull();
        h.LiveOf(SignalKind.Gap, other).Should().NotBeNull();
    }

    [Fact]
    public async Task TheHookDoesNotResolveAggregates()
    {
        // Aggregate membership is a portfolio-wide fact, not something one gesture
        // settles — and they carry no subject to be touched in the first place.
        var h = SignalDetectionHarness.Create();
        var dead = h.SeedProject("Omega", plannedStart: Today, plannedEnd: Today.AddDays(30), cancelled: true);
        var stranded = h.SeedDemand(TimeSpan.FromHours(40), nodeId: dead);
        await h.SweepAsync();

        await h.Detector.ResolveStaleAsync(new SignalTouch([stranded], [], []), Today);

        h.LiveOf(SignalKind.DemandOnClosedRoot).Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmingATentativeResolvesItsBreach()
    {
        // The dashboard's inline "Conferma allocazione": a state change, no
        // quantity moved — and the row must go immediately.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        var coverageId = h.SeedCoverage(
            demandId, Today, Today.AddDays(10), status: AllocationStatus.Tentative);
        await h.SweepAsync();

        h.LiveOf(SignalKind.TentativeInFrozen, coverageId).Should().NotBeNull();

        var coverage = h.Db.Allocations.Single(a => a.Id == coverageId);
        coverage.ChangeStatus(AllocationStatus.Hard);
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        await h.Detector.ResolveStaleAsync(new SignalTouch([], [coverageId], []), Today);

        h.LiveOf(SignalKind.TentativeInFrozen, coverageId).Should().BeNull();
    }

    // ── Sweep state ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TheSweepRecordsItself_SoAnEmptyQueueCanBeToldFromNeverHavingLooked()
    {
        var h = SignalDetectionHarness.Create();

        h.Db.SignalSweepStates.Should().BeEmpty();   // never swept: the honest state

        var result = await h.SweepAsync();

        var state = h.SweepState();
        state.LastSweptAt.Should().NotBeNull();
        state.LastLiveCount.Should().Be(result.Value!.LiveCount);
    }

    [Fact]
    public async Task ResolvedSignalsBeyondTheRetentionWindow_ArePurged()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));

        await h.SweepAsync();
        var signalId = h.LiveOf(SignalKind.Gap, demandId)!.Id;

        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(49), percent: 100m);
        await h.SweepAsync();
        h.Reload(signalId).Detection.Should().Be(SignalDetection.Resolved);

        // Retention down to a day, then the clock past it. Resolved rows ARE the
        // change feed's history, so this window is depth, not housekeeping.
        var policy = await h.Policy.GetConfigurationAsync();
        var tracked = h.Db.SignalPolicies.Single(p => p.Id == policy.Id);
        tracked.Replace(1, Duration.Of(2, DurationUnit.Weeks));
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        h.Clock.Advance(TimeSpan.FromDays(10));
        var result = await h.SweepAsync();

        result.Value!.Purged.Should().Be(1);
        h.Db.PlanSignals.AsNoTracking().Any(s => s.Id == signalId).Should().BeFalse();
    }
}
