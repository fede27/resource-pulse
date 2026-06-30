using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Application-layer behaviour of the plan command envelope (ADR-0018/0019):
// dispatch by type, dryRun has no persistent effects (explicit), cascade gaps,
// overlap sums and does not error, "remove person mid-span" composition.
public class PlanCommandServiceTests
{
    private static readonly DateOnly D1 = new(2026, 6, 1);
    private static readonly DateOnly D14 = new(2026, 6, 14);

    // ── Dispatch by type ──────────────────────────────────────────────────────

    [Fact]
    public async Task SplitAt_DispatchesAndProducesTwoAdjacentBlocks()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);

        var result = await h.Service.ExecuteAsync(new SplitAtCommand { Id = id, Date = new DateOnly(2026, 6, 8) });

        result.IsSuccess.Should().BeTrue();
        result.Value.CommandKind.Should().Be("splitAt");
        result.Value.Committed.Should().BeTrue();
        result.Value.Changes.Should().HaveCount(2);

        var first = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Modified);
        var second = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Created);
        first.PeriodEnd.Should().Be(new DateOnly(2026, 6, 7));
        second.PeriodStart.Should().Be(new DateOnly(2026, 6, 8));
        second.PeriodEnd.Should().Be(D14);
        h.AllocationCount().Should().Be(2);
    }

    [Fact]
    public async Task Create_PersistsNewAssignedBlock()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            ResourceId = h.ResourceId,
            ProjectNodeId = h.ProjectNodeId,
            PeriodStart = D1,
            PeriodEnd = D14,
            Percent = 40m
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Should().ContainSingle().Which.Kind.Should().Be(PlanChangeKind.Created);
        h.AllocationCount().Should().Be(1);
    }

    [Fact]
    public async Task Move_ShiftsBothEdgesByDelta()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);

        var result = await h.Service.ExecuteAsync(new MoveCommand { Id = id, DeltaDays = 7 });

        result.IsSuccess.Should().BeTrue();
        var moved = h.Reload(id);
        moved.PeriodStart.Should().Be(D1.AddDays(7));
        moved.PeriodEnd.Should().Be(D14.AddDays(7));
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14);

        var result = await h.Service.ExecuteAsync(new DeleteCommand { Id = id });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Should().ContainSingle().Which.Kind.Should().Be(PlanChangeKind.Deleted);
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task ChangeRateFrom_ProducesPiecewiseProfile()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);

        var result = await h.Service.ExecuteAsync(new ChangeRateFromCommand
        {
            Id = id, Date = new DateOnly(2026, 6, 8), NewRate = 80m
        });

        result.IsSuccess.Should().BeTrue();
        var first = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Modified);
        var second = result.Value.Changes.Single(c => c.Kind == PlanChangeKind.Created);
        first.AllocationPercent.Should().Be(50m);
        first.PeriodEnd.Should().Be(new DateOnly(2026, 6, 7));
        second.AllocationPercent.Should().Be(80m);
        second.PeriodStart.Should().Be(new DateOnly(2026, 6, 8));
    }

    [Fact]
    public async Task Unknown_NodeOnCreate_ReturnsValidation()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            ResourceId = h.ResourceId,
            ProjectNodeId = Guid.NewGuid(), // does not exist
            PeriodStart = D1, PeriodEnd = D14, Percent = 10m
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Validation);
    }

    [Fact]
    public async Task ChangeStatus_HardOnExploratoryProject_ConflictsViaI6()
    {
        var h = PlanCommandHarness.Create(commitment: CommitmentLevel.Exploratory);
        var id = h.SeedAllocation(D1, D14);

        var result = await h.Service.ExecuteAsync(new ChangeStatusCommand
        {
            Id = id, Status = AllocationStatus.Hard
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
        h.Reload(id).Status.Should().Be(AllocationStatus.Tentative);
    }

    // ── dryRun: no persistent effects (explicit) ──────────────────────────────

    [Fact]
    public async Task DryRun_SplitAt_ComputesConsequence_ButDoesNotPersist()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);

        var result = await h.Service.ExecuteAsync(new SplitAtCommand
        {
            Id = id, Date = new DateOnly(2026, 6, 8), DryRun = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.DryRun.Should().BeTrue();
        result.Value.Committed.Should().BeFalse();
        result.Value.Changes.Should().HaveCount(2); // consequence computed

        // …but nothing changed in the store.
        h.AllocationCount().Should().Be(1);
        var unchanged = h.Reload(id);
        unchanged.PeriodStart.Should().Be(D1);
        unchanged.PeriodEnd.Should().Be(D14);
    }

    [Fact]
    public async Task DryRun_Create_DoesNotPersist()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateCommand
        {
            ResourceId = h.ResourceId, ProjectNodeId = h.ProjectNodeId,
            PeriodStart = D1, PeriodEnd = D14, Percent = 30m, DryRun = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Should().ContainSingle();
        h.AllocationCount().Should().Be(0);
    }

    [Fact]
    public async Task DryRun_Delete_DoesNotPersist()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14);

        var result = await h.Service.ExecuteAsync(new DeleteCommand { Id = id, DryRun = true });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Single().Kind.Should().Be(PlanChangeKind.Deleted);
        h.AllocationCount().Should().Be(1); // still there
    }

    [Fact]
    public async Task DryRun_ThenReal_ProducesSameShape_AndRealPersists()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);
        var split = new DateOnly(2026, 6, 8);

        var dry = await h.Service.ExecuteAsync(new SplitAtCommand { Id = id, Date = split, DryRun = true });
        h.AllocationCount().Should().Be(1);

        var real = await h.Service.ExecuteAsync(new SplitAtCommand { Id = id, Date = split, DryRun = false });
        h.AllocationCount().Should().Be(2);

        dry.Value.Changes.Should().HaveCount(real.Value.Changes.Count);
    }

    // ── Cascade: gaps preserved, overlap sums and does not error ───────────────

    [Fact]
    public async Task ShiftFrom_PreservesRelativeGaps_OnLane()
    {
        var h = PlanCommandHarness.Create();
        var a = h.SeedAllocation(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5));
        var b = h.SeedAllocation(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12));
        var c = h.SeedAllocation(new DateOnly(2026, 6, 20), new DateOnly(2026, 6, 25));

        var result = await h.Service.ExecuteAsync(new ShiftFromCommand
        {
            ResourceId = h.ResourceId, ProjectNodeId = h.ProjectNodeId,
            FromDate = new DateOnly(2026, 6, 1), DeltaDays = 9
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Should().HaveCount(3);

        var ra = h.Reload(a); var rb = h.Reload(b); var rc = h.Reload(c);
        ra.PeriodStart.Should().Be(new DateOnly(2026, 6, 10));
        // gaps (in days between end of one and start of next) preserved
        (rb.PeriodStart.DayNumber - ra.PeriodEnd.DayNumber).Should().Be(5);
        (rc.PeriodStart.DayNumber - rb.PeriodEnd.DayNumber).Should().Be(8);
    }

    [Fact]
    public async Task ShiftFrom_OnlyShiftsBlocksAtOrAfterFromDate()
    {
        var h = PlanCommandHarness.Create();
        var early = h.SeedAllocation(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5));
        var late = h.SeedAllocation(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12));

        await h.Service.ExecuteAsync(new ShiftFromCommand
        {
            ResourceId = h.ResourceId, ProjectNodeId = h.ProjectNodeId,
            FromDate = new DateOnly(2026, 6, 10), DeltaDays = 3
        });

        h.Reload(early).PeriodStart.Should().Be(new DateOnly(2026, 6, 1)); // untouched
        h.Reload(late).PeriodStart.Should().Be(new DateOnly(2026, 6, 13)); // shifted
    }

    [Fact]
    public async Task Resize_IntoOverlap_Succeeds_NoEnforcement()
    {
        var h = PlanCommandHarness.Create();
        var first = h.SeedAllocation(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), 50m);
        h.SeedAllocation(new DateOnly(2026, 6, 6), new DateOnly(2026, 6, 10), 50m);

        // Resize the first to overlap the second — allowed, sums (ADR-0014).
        var result = await h.Service.ExecuteAsync(new ResizeCommand
        {
            Id = first, NewPeriodEnd = new DateOnly(2026, 6, 8)
        });

        result.IsSuccess.Should().BeTrue();
        h.Reload(first).PeriodEnd.Should().Be(new DateOnly(2026, 6, 8));
    }

    // ── "Remove person mid-span" = splitAt + convertToPlaceholder (composition) ─

    [Fact]
    public async Task RemovePersonMidSpan_TwoCommands_SecondBlockBecomesPlaceholder()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedAllocation(D1, D14, 50m);
        var split = new DateOnly(2026, 6, 8);

        var splitResult = await h.Service.ExecuteAsync(new SplitAtCommand { Id = id, Date = split });
        var secondId = splitResult.Value.Changes.Single(c => c.Kind == PlanChangeKind.Created).Id;

        var convert = await h.Service.ExecuteAsync(new ConvertToPlaceholderCommand
        {
            Id = secondId, RoleId = h.RoleId
        });

        convert.IsSuccess.Should().BeTrue();
        // First half still assigned, second half is now an open role.
        h.Reload(id).IsPlaceholder.Should().BeFalse();
        var second = h.Reload(secondId);
        second.IsPlaceholder.Should().BeTrue();
        second.RoleId.Should().Be(h.RoleId);
        second.PeriodStart.Should().Be(split);
    }

    // ── createByHours / retarget use capacity (8h/day fixture) ─────────────────

    [Fact]
    public async Task CreateByHours_ResolvesPercentFromCapacity()
    {
        // 14 days × 8h = 112h capacity; 56h target ⇒ 50%.
        var h = PlanCommandHarness.Create(capacityPerDay: TimeSpan.FromHours(8));

        var result = await h.Service.ExecuteAsync(new CreateByHoursCommand
        {
            ResourceId = h.ResourceId, ProjectNodeId = h.ProjectNodeId,
            PeriodStart = D1, PeriodEnd = D14, TargetHours = TimeSpan.FromHours(56)
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Changes.Single().AllocationPercent.Should().Be(50m);
    }

    [Fact]
    public async Task Retarget_KeepHours_OnPlaceholder_Conflicts()
    {
        var h = PlanCommandHarness.Create();
        var create = await h.Service.ExecuteAsync(new CreatePlaceholderCommand
        {
            ProjectNodeId = h.ProjectNodeId, PeriodStart = D1, PeriodEnd = D14,
            Percent = 50m, RoleId = h.RoleId
        });
        var id = create.Value.Changes.Single().Id;

        var result = await h.Service.ExecuteAsync(new RetargetCommand
        {
            Id = id, NewPeriodStart = D1.AddDays(7), NewPeriodEnd = D14.AddDays(7), Mode = MoveMode.KeepHours
        });

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ServiceErrorKind.Conflict);
    }
}
