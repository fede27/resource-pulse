using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class SignalSweepStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ANewTenantHasNeverBeenSwept_AndThatNullIsTheState()
    {
        // The whole reason this aggregate exists (ADR-0032 §10): an empty
        // plan_signals table cannot tell "il piano tiene" from "non abbiamo
        // ancora guardato", and on a brand-new tenant the first reading is never
        // the true one.
        var state = SignalSweepState.CreateUnswept();

        state.LastSweptAt.Should().BeNull();
        state.LastLiveCount.Should().Be(0);
    }

    [Fact]
    public void NeverSwept_IsNotStale_BecauseItIsADifferentStateEntirely()
    {
        // "Mai guardato" must not be reported as "stantio": the page renders three
        // distinct states, not two.
        SignalSweepState.CreateUnswept()
            .IsStale(Now, TimeSpan.FromDays(2)).Should().BeFalse();
    }

    [Fact]
    public void RecordSweep_StoresTheInstantAndTheLiveCount()
    {
        var state = SignalSweepState.CreateUnswept();

        state.RecordSweep(Now, liveCount: 12);

        state.LastSweptAt.Should().Be(Now);
        state.LastLiveCount.Should().Be(12);
    }

    [Fact]
    public void StalenessIsMeasuredAgainstACallerSuppliedTolerance()
    {
        // The aggregate does not own the cadence — the worker's configuration
        // does, so the tolerance comes from the caller.
        var state = SignalSweepState.CreateUnswept();
        state.RecordSweep(Now.AddDays(-6), liveCount: 3);

        state.IsStale(Now, TimeSpan.FromDays(2)).Should().BeTrue();
        state.IsStale(Now, TimeSpan.FromDays(10)).Should().BeFalse();
    }

    [Fact]
    public void NegativeLiveCount_IsRejected()
    {
        var act = () => SignalSweepState.CreateUnswept().RecordSweep(Now, -1);
        act.Should().Throw<DomainException>();
    }
}
