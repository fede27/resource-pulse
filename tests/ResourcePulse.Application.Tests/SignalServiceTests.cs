using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.Application.Tests;

// The triage read model and the two human gestures (ADR-0032 §13).
public class SignalServiceTests
{
    private static readonly DateOnly Today = SignalDetectionHarness.Today;

    // ── Ranking and shape ────────────────────────────────────────────────────

    [Fact]
    public async Task TheQueueIsRankedServerSide()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(400), decideBy: Today.AddDays(40));   // slushy
        var overdue = h.SeedDemand(TimeSpan.FromHours(10), decideBy: Today.AddDays(-2));
        await h.SweepAsync();

        var result = await h.ServiceAs().GetAsync(SignalScope.All);

        result.Value!.First().SubjectId.Should().Be(overdue);
        result.Value!.First().Zone.Should().Be(SignalZone.Overdue);
    }

    [Fact]
    public async Task LabelsAreResolvedSoTheClientCanComposeATitle()
    {
        // The DTO carries the PARTS, never a sentence: translation lives in the
        // frontend locales, so a server-composed title would be untranslatable.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var gap = (await h.ServiceAs().GetAsync(SignalScope.All)).Value!
            .Single(s => s.Kind == SignalKind.Gap);

        gap.RoleName.Should().Be("Backend");
        gap.RootProjectName.Should().Be("Alpha");
        gap.Link.DemandId.Should().Be(demandId);
        gap.Link.RootProjectId.Should().Be(h.ProjectId);
    }

    [Fact]
    public async Task AnOvercommitCarriesItsDecomposition_NotACandidateMove()
    {
        // The expander shows WHAT MAKES UP the breach. Picking who should cover is
        // a staffing gesture and belongs on the page with the context — and the
        // prototype's "donor project" was invented outright.
        var h = SignalDetectionHarness.Create();
        var beta = h.SeedProject("Beta", plannedStart: Today, plannedEnd: Today.AddDays(90));
        var d1 = h.SeedDemand(TimeSpan.FromHours(400));
        var d2 = h.SeedDemand(TimeSpan.FromHours(400), nodeId: beta);
        h.SeedCoverage(d1, Today, Today.AddDays(30), percent: 80m);
        h.SeedCoverage(d2, Today, Today.AddDays(30), percent: 60m);
        await h.SweepAsync();

        var over = (await h.ServiceAs().GetAsync(SignalScope.All)).Value!
            .Single(s => s.Kind == SignalKind.Overcommit);

        over.ResourceName.Should().Be("Tizio");
        over.Contributions.Select(c => c.RootProjectName).Should().BeEquivalentTo(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task AggregateMembersAreNamedAlphabetically_AndCarryNoFigure()
    {
        var h = SignalDetectionHarness.Create();
        var zoe = h.SeedResource("Zoe Rossi");
        var anna = h.SeedResource("Anna Bianchi");
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        h.SeedCoverage(demandId, Today, Today.AddDays(30), percent: 20m, resourceId: zoe);
        h.SeedCoverage(demandId, Today, Today.AddDays(30), percent: 30m, resourceId: anna);
        await h.SweepAsync();

        var slack = (await h.ServiceAs().GetAsync(SignalScope.All)).Value!
            .Single(s => s.Kind == SignalKind.UnderBand);

        slack.MemberNames.Should().Equal("Anna Bianchi", "Zoe Rossi");
        slack.Magnitude.Should().Be(2m);   // a count, never a utilization rate
    }

    // ── Scope (ADR-0032 §8) ──────────────────────────────────────────────────

    [Fact]
    public async Task MineIsTheProjectsILead()
    {
        var h = SignalDetectionHarness.Create();
        var beta = h.SeedProject("Beta", plannedStart: Today.AddDays(20), plannedEnd: Today.AddDays(120));

        h.LinkUser(h.ResourceId, "sub-elena");
        h.SetLead(h.ProjectId, h.ResourceId);          // leads Alpha, not Beta

        var mineDemand = h.SeedDemand(TimeSpan.FromHours(40));
        h.SeedDemand(TimeSpan.FromHours(40), nodeId: beta);
        await h.SweepAsync();

        var mine = (await h.ServiceAs("sub-elena").GetAsync(SignalScope.Mine)).Value!;
        var all = (await h.ServiceAs("sub-elena").GetAsync(SignalScope.All)).Value!;

        mine.Where(s => s.Kind == SignalKind.Gap).Select(s => s.SubjectId).Should().Equal(mineDemand);
        all.Count(s => s.Kind == SignalKind.Gap).Should().Be(2);
    }

    [Fact]
    public async Task APersonSubjectSignal_IsVisibleToWhoeverLeadsTheProjectsItTouches()
    {
        // THE reason the scope filters on TouchedRootProjectIds rather than on the
        // signal's own subject: an Overcommit has no project, and without this a PM
        // would never see that the people on their own project are overloaded.
        var h = SignalDetectionHarness.Create();
        var beta = h.SeedProject("Beta", plannedStart: Today, plannedEnd: Today.AddDays(90));
        var pm = h.SeedResource("Elena");
        h.LinkUser(pm, "sub-elena");
        h.SetLead(h.ProjectId, pm);

        var d1 = h.SeedDemand(TimeSpan.FromHours(400));
        var d2 = h.SeedDemand(TimeSpan.FromHours(400), nodeId: beta);
        h.SeedCoverage(d1, Today, Today.AddDays(30), percent: 80m);
        h.SeedCoverage(d2, Today, Today.AddDays(30), percent: 60m);
        await h.SweepAsync();

        var mine = (await h.ServiceAs("sub-elena").GetAsync(SignalScope.Mine)).Value!;

        mine.Should().Contain(s => s.Kind == SignalKind.Overcommit);
    }

    [Fact]
    public async Task ACallerLinkedToNoResource_LeadsNothing_SoMineIsEmpty()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var mine = (await h.ServiceAs("sub-sconosciuto").GetAsync(SignalScope.Mine)).Value!;

        mine.Should().BeEmpty();
    }

    // ── Unseen and the visit marker (ADR-0032 §5) ────────────────────────────

    [Fact]
    public async Task EverythingIsUnseenUntilTheFirstVisit()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        (await svc.GetAsync(SignalScope.All)).Value!.Should().OnlyContain(s => s.IsUnseen);

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await svc.RecordVisitAsync();

        (await svc.GetAsync(SignalScope.All)).Value!.Should().OnlyContain(s => !s.IsUnseen);
    }

    [Fact]
    public async Task ASignalDetectedAfterTheVisit_IsUnseenAgain()
    {
        var h = SignalDetectionHarness.Create();
        var first = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await svc.RecordVisitAsync();

        h.Clock.Advance(TimeSpan.FromHours(1));
        var second = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var rows = (await svc.GetAsync(SignalScope.All)).Value!;
        rows.Single(s => s.SubjectId == first).IsUnseen.Should().BeFalse();
        rows.Single(s => s.SubjectId == second).IsUnseen.Should().BeTrue();
    }

    [Fact]
    public async Task TheVisitMarkerIsPerUser_NotPerSignal()
    {
        // One row per person instead of N: that is what keeping "seen" out of the
        // domain buys (ADR-0032 §5).
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        await h.ServiceAs("sub-a").RecordVisitAsync();
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await h.ServiceAs("sub-a").RecordVisitAsync();
        await h.ServiceAs("sub-b").RecordVisitAsync();

        h.Db.SignalVisits.AsNoTracking().Should().HaveCount(2);
        (await h.ServiceAs("sub-b").GetAsync(SignalScope.All)).Value!
            .Should().OnlyContain(s => !s.IsUnseen);
    }

    // ── Acknowledgement ──────────────────────────────────────────────────────

    [Fact]
    public async Task AcknowledgingKeepsTheRowInTheQueue()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        var signal = (await svc.GetAsync(SignalScope.All)).Value!.First();

        var acked = await svc.AcknowledgeAsync(signal.Id, "il cliente ha confermato lo slittamento");

        acked.Value!.IsAcknowledged.Should().BeTrue();
        acked.Value.AcknowledgedReason.Should().Be("il cliente ha confermato lo slittamento");
        acked.Value.AcknowledgedBy.Should().Be("sub-elena@x");

        // Still there: an assumed risk is not a hidden one.
        (await svc.GetAsync(SignalScope.All)).Value!.Should().Contain(s => s.Id == signal.Id);
    }

    [Fact]
    public async Task ReopeningClearsTheAcknowledgement_AndBothEntriesSurvive()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        var signal = (await svc.GetAsync(SignalScope.All)).Value!.First();

        await svc.AcknowledgeAsync(signal.Id, "va bene così");
        var reopened = await svc.ReopenAsync(signal.Id);

        reopened.Value!.IsAcknowledged.Should().BeFalse();
        h.Db.PlanSignals.AsNoTracking()
            .Include(s => s.Acknowledgements)
            .Single(s => s.Id == signal.Id)
            .Acknowledgements.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcknowledgingAnUnknownSignal_IsNotFound()
    {
        var h = SignalDetectionHarness.Create();

        var result = await h.ServiceAs().AcknowledgeAsync(Guid.NewGuid(), null);

        result.IsSuccess.Should().BeFalse();
    }

    // ── Change feed (ADR-0032 §6) ────────────────────────────────────────────

    [Fact]
    public async Task WithoutAPreviousVisit_TheFeedIsEmpty()
    {
        // Nothing to be "new since" yet — and inventing a window would make the
        // first login look like a flood of changes.
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        (await h.ServiceAs().GetChangesAsync(SignalScope.All, since: null, max: 4))
            .Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task AWorseningSignal_AppearsInTheFeedWithBothFigures()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        var coverageId = h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);
        await h.SweepAsync();

        var svc = h.ServiceAs();
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await svc.RecordVisitAsync();

        h.Clock.Advance(TimeSpan.FromHours(1));
        h.Db.Allocations.Remove(h.Db.Allocations.Single(a => a.Id == coverageId));
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();
        await h.SweepAsync();

        var feed = (await svc.GetChangesAsync(SignalScope.All, since: null, max: 4)).Value!;

        var entry = feed.Single(c => c.Kind == SignalKind.Gap);
        entry.Change.Should().HaveFlag(SignalChange.MagnitudeWorsened);
        entry.PreviousMagnitude.Should().Be(20m);
        entry.Magnitude.Should().Be(40m);
    }

    [Fact]
    public async Task AResolvedSignal_AppearsInTheFeed_EvenThoughItLeftTheQueue()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await svc.RecordVisitAsync();

        h.Clock.Advance(TimeSpan.FromHours(1));
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(49), percent: 100m);
        await h.SweepAsync();

        var feed = (await svc.GetChangesAsync(SignalScope.All, since: null, max: 4)).Value!;

        feed.Should().Contain(c => c.Change == SignalChange.Resolved);
        (await svc.GetAsync(SignalScope.All)).Value!.Should().NotContain(s => s.SubjectId == demandId);
    }

    [Fact]
    public async Task AnImprovementStaysOutOfTheFeed()
    {
        // "Non voglio rumore": the data is updated, the feed is quiet.
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var svc = h.ServiceAs();
        var signalId = (await svc.GetAsync(SignalScope.All)).Value!
            .Single(s => s.Kind == SignalKind.Gap).Id;

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await svc.RecordVisitAsync();

        h.Clock.Advance(TimeSpan.FromHours(1));
        h.SeedCoverage(demandId, Today.AddDays(30), Today.AddDays(34), percent: 50m);  // gap 40h → 20h
        await h.SweepAsync();

        var feed = (await svc.GetChangesAsync(SignalScope.All, since: null, max: 4)).Value!;

        // The gap is quiet. (The feed is not necessarily empty: putting someone at
        // 50% also puts them under the healthy band, and THAT is a genuinely new
        // signal — worth seeing, and a good check that the filter is per-signal
        // rather than a blanket mute.)
        feed.Should().NotContain(c => c.SignalId == signalId);
        (await svc.GetAsync(SignalScope.All)).Value!.Single(s => s.SubjectId == demandId)
            .Magnitude.Should().Be(20m);
    }

    // ── Sweep state (ADR-0032 §10) ───────────────────────────────────────────

    [Fact]
    public async Task BeforeTheFirstSweep_LastSweptAtIsNull_SoAnEmptyQueueIsNotAClaim()
    {
        var h = SignalDetectionHarness.Create();

        var sweep = (await h.ServiceAs().GetSweepAsync()).Value!;

        sweep.LastSweptAt.Should().BeNull();
        sweep.QueueBudget.Should().Be(SignalRanking.QueueBudget);
        sweep.StaleAfterHours.Should().Be(48);
    }

    [Fact]
    public async Task AfterASweep_TheStateCarriesTheInstantAndTheLiveCount()
    {
        var h = SignalDetectionHarness.Create();
        h.SeedDemand(TimeSpan.FromHours(40));
        await h.SweepAsync();

        var sweep = (await h.ServiceAs().GetSweepAsync()).Value!;

        sweep.LastSweptAt.Should().NotBeNull();
        sweep.LiveCount.Should().Be(1);
    }

    [Fact]
    public async Task TheSweepStateCarriesThisUsersPreviousVisit()
    {
        var h = SignalDetectionHarness.Create();
        await h.SweepAsync();

        var svc = h.ServiceAs();
        await svc.RecordVisitAsync();

        (await svc.GetSweepAsync()).Value!.LastVisitedAt.Should().Be(h.Clock.GetUtcNow());
    }

    // ── Tentative labels ─────────────────────────────────────────────────────

    [Fact]
    public async Task ATentativeInFrozen_ResolvesBothThePersonAndTheRoleAsked()
    {
        var h = SignalDetectionHarness.Create();
        var demandId = h.SeedDemand(TimeSpan.FromHours(400));
        var coverageId = h.SeedCoverage(
            demandId, Today, Today.AddDays(10), status: AllocationStatus.Tentative);
        await h.SweepAsync();

        var signal = (await h.ServiceAs().GetAsync(SignalScope.All)).Value!
            .Single(s => s.Kind == SignalKind.TentativeInFrozen);

        signal.ResourceName.Should().Be("Tizio");
        signal.RoleName.Should().Be("Backend");
        signal.Link.AllocationId.Should().Be(coverageId);
    }
}
