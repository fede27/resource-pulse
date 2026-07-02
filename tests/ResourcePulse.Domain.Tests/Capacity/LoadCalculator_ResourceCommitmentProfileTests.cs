namespace ResourcePulse.Domain.Tests.Capacity;

// Resource commitment profile (gap #4+#10 / ADR-0023): run-length segments of a
// person's committed rate% over the horizon, decomposed by root project. Pure,
// capacity-independent: percent = sum of active assigned rate%.
public class LoadCalculator_ResourceCommitmentProfileTests
{
    private static readonly Guid R = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    // Two projects; project B carries a phase node distinct from its root.
    private static readonly Guid RootA = Guid.NewGuid();
    private static readonly Guid RootB = Guid.NewGuid();
    private static readonly Guid PhaseB = Guid.NewGuid();

    private static readonly Dictionary<Guid, Guid> RootMap = new()
    {
        [RootA] = RootA,
        [RootB] = RootB,
        [PhaseB] = RootB, // a phase rolls up to its root project
    };

    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Tue = new(2026, 6, 2);
    private static readonly DateOnly Wed = new(2026, 6, 3);
    private static readonly DateOnly Thu = new(2026, 6, 4);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private static IReadOnlyList<LoadSegment> Profile(params Allocation[] allocations) =>
        LoadCalculator.ResourceCommitmentProfile(R, allocations, RootMap, Mon, Fri);

    [Fact]
    public void SingleAllocation_FullHorizon_OneSegment()
    {
        var a = Allocation.Create(R, RootA, Mon, Fri, 50m);

        var segments = Profile(a);

        segments.Should().ContainSingle();
        var s = segments[0];
        s.From.Should().Be(Mon);
        s.To.Should().Be(Fri);
        s.Percent.Should().Be(50m);
        s.ByProject.Should().HaveCount(1);
        s.ByProject[RootA].Should().Be(50m);
    }

    [Fact]
    public void OverlappingProjects_SplitsAtBoundaries_AndSumsInOverlap()
    {
        var onA = Allocation.Create(R, RootA, Mon, Fri, 50m);   // whole week
        var onB = Allocation.Create(R, PhaseB, Wed, Thu, 30m);  // phase of B, mid-week

        var segments = Profile(onA, onB);

        segments.Should().HaveCount(3);

        segments[0].From.Should().Be(Mon);
        segments[0].To.Should().Be(Tue);
        segments[0].Percent.Should().Be(50m);
        segments[0].ByProject.Should().HaveCount(1);

        segments[1].From.Should().Be(Wed);
        segments[1].To.Should().Be(Thu);
        segments[1].Percent.Should().Be(80m);
        segments[1].ByProject[RootA].Should().Be(50m);
        segments[1].ByProject[RootB].Should().Be(30m); // rolled up from PhaseB

        segments[2].From.Should().Be(Fri);
        segments[2].To.Should().Be(Fri);
        segments[2].Percent.Should().Be(50m);
    }

    [Fact]
    public void Overcommitment_AboveHundred_IsFirstClass()
    {
        var a1 = Allocation.Create(R, RootA, Mon, Fri, 70m);
        var a2 = Allocation.Create(R, RootB, Mon, Fri, 60m);

        var segments = Profile(a1, a2);

        segments.Should().ContainSingle();
        segments[0].Percent.Should().Be(130m); // > 100, no clamp
        segments[0].ByProject[RootA].Should().Be(70m);
        segments[0].ByProject[RootB].Should().Be(60m);
    }

    [Fact]
    public void SharesSumToSegmentPercent()
    {
        var a1 = Allocation.Create(R, RootA, Mon, Fri, 25m);
        var a2 = Allocation.Create(R, PhaseB, Mon, Fri, 40m);

        var s = Profile(a1, a2).Should().ContainSingle().Subject;

        s.ByProject.Values.Sum().Should().Be(s.Percent);
    }

    [Fact]
    public void SameResource_TwoBlocksSameProject_SumIntoOneShare()
    {
        // Two blocks on the same root project sum into a single share (ADR-0014).
        var baseBlock = Allocation.Create(R, RootA, Mon, Fri, 50m);
        var bump = Allocation.Create(R, RootA, Mon, Fri, 20m);

        var s = Profile(baseBlock, bump).Should().ContainSingle().Subject;

        s.ByProject.Should().HaveCount(1);
        s.ByProject[RootA].Should().Be(70m);
        s.Percent.Should().Be(70m);
    }

    [Fact]
    public void OtherResource_NotCounted()
    {
        var mine = Allocation.Create(R, RootA, Mon, Fri, 50m);
        var theirs = Allocation.Create(Other, RootA, Mon, Fri, 90m);

        var s = Profile(mine, theirs).Should().ContainSingle().Subject;

        s.Percent.Should().Be(50m); // theirs excluded
    }

    [Fact]
    public void Placeholder_Excluded()
    {
        // A placeholder has no ResourceId, so it is not part of any person's profile.
        var mine = Allocation.Create(R, RootA, Mon, Fri, 50m);
        var hole = Allocation.CreatePlaceholder(RootB, Mon, Fri, 40m, Guid.NewGuid(), null);

        var s = Profile(mine, hole).Should().ContainSingle().Subject;

        s.Percent.Should().Be(50m);
        s.ByProject.Should().HaveCount(1);
    }

    [Fact]
    public void EmptyHorizon_OneZeroSegmentCoveringRange()
    {
        var segments = Profile(); // no allocations

        segments.Should().ContainSingle();
        segments[0].From.Should().Be(Mon);
        segments[0].To.Should().Be(Fri);
        segments[0].Percent.Should().Be(0m);
        segments[0].ByProject.Should().BeEmpty();
    }

    [Fact]
    public void LeadingAndTrailingZero_AreTheirOwnSegments()
    {
        // Allocation only Wed-Thu: Mon-Tue zero, Wed-Thu 100, Fri zero.
        var a = Allocation.Create(R, RootA, Wed, Thu, 100m);

        var segments = Profile(a);

        segments.Should().HaveCount(3);
        segments[0].Percent.Should().Be(0m);
        segments[1].Percent.Should().Be(100m);
        segments[2].Percent.Should().Be(0m);

        // Peak is a trivial caller derivation, not a field.
        segments.Max(s => s.Percent).Should().Be(100m);
    }

    [Fact]
    public void FromAfterTo_YieldsNothing()
    {
        var a = Allocation.Create(R, RootA, Mon, Fri, 50m);

        var segments = LoadCalculator.ResourceCommitmentProfile(R, [a], RootMap, Fri, Mon);

        segments.Should().BeEmpty();
    }

    [Fact]
    public void NodeMissingFromMap_FallsBackToOwnId()
    {
        var unknownNode = Guid.NewGuid(); // not in RootMap
        var a = Allocation.Create(R, unknownNode, Mon, Fri, 50m);

        var s = Profile(a).Should().ContainSingle().Subject;

        s.ByProject.Should().ContainKey(unknownNode);
        s.ByProject[unknownNode].Should().Be(50m);
    }
}
