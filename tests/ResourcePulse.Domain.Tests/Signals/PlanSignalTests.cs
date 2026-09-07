using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class PlanSignalTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);
    private static readonly DateTimeOffset T0 = new(2026, 6, 24, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddDays(1);

    private static FenceBoundaries Boundaries() =>
        TimeFenceConfiguration
            .Create(Guid.NewGuid(), Duration.Of(2, DurationUnit.Weeks), Duration.Of(2, DurationUnit.Months))
            .ComputeBoundaries(Today);

    private static SignalObservation Obs(
        decimal magnitude,
        DateOnly? deadline = null,
        bool hardCommitted = false,
        IReadOnlyList<Guid>? touched = null,
        IReadOnlyList<Guid>? members = null) =>
        SignalObservation.For(deadline, Today, Boundaries(), magnitude, hardCommitted, touched, members);

    // ── Identity and shape ────────────────────────────────────────────────────

    [Fact]
    public void PerSubjectKind_RequiresASubject()
    {
        var act = () => PlanSignal.Detect(SignalKind.Gap, subjectId: null, Obs(40m), T0);
        act.Should().Throw<DomainException>().WithMessage("*per-subject*");
    }

    [Fact]
    public void AggregateKind_RejectsASubject()
    {
        // Persisting a row per person for UnderBand would BE the per-person
        // utilization ranking the aggregate exists to avoid.
        var act = () => PlanSignal.Detect(SignalKind.UnderBand, Guid.NewGuid(), Obs(3m), T0);
        act.Should().Throw<DomainException>().WithMessage("*aggregated*");
    }

    [Fact]
    public void Detect_StartsLive_AndRecordsCreation()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m, Today.AddDays(3)), T0);

        signal.IsLive.Should().BeTrue();
        signal.Tier.Should().Be(SignalTier.Breach);
        signal.Shape.Should().Be(SignalShape.Subject);
        signal.FirstDetectedAt.Should().Be(T0);
        signal.LastChange.Should().Be(SignalChange.Created);
        signal.Zone.Should().Be(SignalZone.Frozen);
        signal.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public void Observation_ComputesTheZoneFromTheDeadline_SoTheyCannotDiverge()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(10m, Today.AddDays(-2)), T0);

        signal.DeadlineAt.Should().Be(Today.AddDays(-2));
        signal.Zone.Should().Be(SignalZone.Overdue);
    }

    // ── Observation: only worsening reaches the feed ──────────────────────────

    [Fact]
    public void GrowingMagnitude_Worsens_AndKeepsThePreviousValue()
    {
        var signal = PlanSignal.Detect(SignalKind.Overcommit, Guid.NewGuid(), Obs(105m), T0);

        var change = signal.Observe(Obs(120m), T1);

        change.Should().Be(SignalChange.MagnitudeWorsened);
        signal.Magnitude.Should().Be(120m);
        signal.PreviousMagnitude.Should().Be(105m);   // "da 105% a 120%"
        signal.LastChangedAt.Should().Be(T1);
    }

    [Fact]
    public void ShrinkingMagnitude_UpdatesTheDataButStaysOutOfTheFeed()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(80m), T0);

        var change = signal.Observe(Obs(20m), T1);

        change.Should().Be(SignalChange.None);
        signal.Magnitude.Should().Be(20m);            // data always current
        signal.LastChangedAt.Should().Be(T0);         // feed untouched
        signal.LastObservedAt.Should().Be(T1);
    }

    [Fact]
    public void DeadlineSlidingFurtherOut_IsSilent_ButTheDeadlineIsStillCorrected()
    {
        // Pinning the deadline at first detection was the alternative, and it lies:
        // the signal would keep claiming a stale date and stay Overdue for good.
        var signal = PlanSignal.Detect(SignalKind.Overcommit, Guid.NewGuid(), Obs(115m, Today.AddDays(3)), T0);

        var change = signal.Observe(Obs(115m, Today.AddDays(40)), T1);

        change.Should().Be(SignalChange.None);
        signal.Zone.Should().Be(SignalZone.Slushy);
        signal.DeadlineAt.Should().Be(Today.AddDays(40));
    }

    [Fact]
    public void CrossingIntoTheFrozenZone_Worsens()
    {
        var signal = PlanSignal.Detect(SignalKind.TentativeInFrozen, Guid.NewGuid(), Obs(30m, Today.AddDays(40)), T0);

        var change = signal.Observe(Obs(30m, Today.AddDays(3)), T1);

        change.Should().Be(SignalChange.ZoneWorsened);
        signal.PreviousZone.Should().Be(SignalZone.Slushy);
        signal.Zone.Should().Be(SignalZone.Frozen);
    }

    [Fact]
    public void MagnitudeAndZoneCanWorsenTogether()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(10m, Today.AddDays(40)), T0);

        var change = signal.Observe(Obs(60m, Today.AddDays(2)), T1);

        change.Should().HaveFlag(SignalChange.MagnitudeWorsened);
        change.Should().HaveFlag(SignalChange.ZoneWorsened);
    }

    // ── Resolution ────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_IsIdempotent_BecauseRepeatedSweepsAreNormal()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);
        signal.Resolve(T1);
        signal.Resolve(T1.AddDays(1));

        signal.ResolvedAt.Should().Be(T1);
        signal.LastChange.Should().Be(SignalChange.Resolved);
        signal.IsLive.Should().BeFalse();
    }

    [Fact]
    public void ResolvedSignal_CannotBeReObserved()
    {
        // A returning condition is a NEW row (ADR-0032 §2). That is what keeps
        // FirstDetectedAt unambiguous for the unseen dot.
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);
        signal.Resolve(T1);

        var act = () => signal.Observe(Obs(50m), T1.AddDays(1));
        act.Should().Throw<DomainException>().WithMessage("*historical record*");
    }

    // ── Acknowledgement: the human axis ───────────────────────────────────────

    [Fact]
    public void Acknowledge_DoesNotResolve_AndKeepsTheReason()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);

        signal.Acknowledge("elena@acme.example", "il cliente ha confermato lo slittamento", T1);

        signal.IsAcknowledged.Should().BeTrue();
        signal.IsLive.Should().BeTrue();               // stays in the queue
        signal.CurrentAcknowledgement!.Reason.Should().Be("il cliente ha confermato lo slittamento");
        signal.CurrentAcknowledgement.By.Should().Be("elena@acme.example");
    }

    [Fact]
    public void AcceptReopenAccept_AppendsThreeEntries()
    {
        // Append-only is the reason this is a collection and not three fields: the
        // cycle is real (the dashboard has a "Riapri" button) and the audit of an
        // organizational decision has to survive it.
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);

        signal.Acknowledge("anna", "ok per ora", T0);
        signal.Reopen("luca", T0.AddHours(1));
        signal.Acknowledge("anna", "confermato di nuovo", T0.AddHours(2));

        signal.Acknowledgements.Should().HaveCount(3);
        signal.Acknowledgements.Select(a => a.Sequence).Should().Equal(1, 2, 3);
        signal.Acknowledgements.Select(a => a.Action).Should().Equal(
            AcknowledgementAction.Accept, AcknowledgementAction.Reopen, AcknowledgementAction.Accept);
        signal.IsAcknowledged.Should().BeTrue();
        signal.CurrentAcknowledgement!.Reason.Should().Be("confermato di nuovo");
    }

    [Fact]
    public void Reopen_ClearsTheAcknowledgedState()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);
        signal.Acknowledge("anna", null, T0);

        signal.Reopen("anna", T1);

        signal.IsAcknowledged.Should().BeFalse();
        signal.CurrentAcknowledgement.Should().BeNull();
    }

    [Fact]
    public void ReopenCarriesNoReason_EvenIfOneIsOffered() =>
        SignalAcknowledgement.Create(1, AcknowledgementAction.Reopen, T0, "anna", "qualcosa")
            .Reason.Should().BeNull();

    [Fact]
    public void ResolvedSignal_CarriesNoRiskToAccept()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);
        signal.Resolve(T1);

        var act = () => signal.Acknowledge("anna", null, T1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AcknowledgeAndReopen_AreIdempotent()
    {
        var signal = PlanSignal.Detect(SignalKind.Gap, Guid.NewGuid(), Obs(40m), T0);

        signal.Acknowledge("anna", "una volta", T0);
        signal.Acknowledge("anna", "due volte", T1);
        signal.Reopen("anna", T1);
        signal.Reopen("anna", T1.AddHours(1));

        signal.Acknowledgements.Should().HaveCount(2);
    }

    // ── Payload ───────────────────────────────────────────────────────────────

    [Fact]
    public void TouchedProjects_AreDeduplicated_AndEmptyGuidsDropped()
    {
        var acme = Guid.NewGuid();
        var beta = Guid.NewGuid();

        var signal = PlanSignal.Detect(
            SignalKind.Overcommit, Guid.NewGuid(),
            Obs(120m, touched: [acme, beta, acme, Guid.Empty]), T0);

        signal.TouchedRootProjectIds.Should().BeEquivalentTo([acme, beta]);
    }

    [Fact]
    public void AggregateMembers_AreCarriedAsAnUnorderedPayload()
    {
        var people = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var signal = PlanSignal.Detect(SignalKind.UnderBand, null, Obs(3m, members: people), T0);

        signal.MemberSubjectIds.Should().BeEquivalentTo(people);
        signal.Magnitude.Should().Be(3m); // a cardinality, never a utilization rate
    }

    [Fact]
    public void NegativeMagnitude_IsRejected()
    {
        var act = () => Obs(-1m);
        act.Should().Throw<DomainException>();
    }
}
