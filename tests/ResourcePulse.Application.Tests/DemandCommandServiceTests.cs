using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

// Application-layer behaviour of the demand command kinds (Phase 5.0):
// createDemand / editDemand / deleteDemand, dryRun no-commit, C2 role correction,
// I1/I4 guards. NOTE: PlanCommandHarness.Create() seeds ONE default demand, so
// counts are measured against that baseline (1).
public class DemandCommandServiceTests
{
    // ── createDemand ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDemand_PersistsDeclaredDemand()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateDemandCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = h.RoleId,
            RequiredHours = TimeSpan.FromHours(40)
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.CommandKind.Should().Be("createDemand");
        result.Value.DemandChanges.Should().ContainSingle()
            .Which.Should().Match<PlanDemandChange>(c =>
                c.Kind == PlanChangeKind.Created && c.Provenance == DemandProvenance.Declared);
        h.DemandCount().Should().Be(2); // 1 baseline + 1 created
    }

    [Fact]
    public async Task CreateDemand_NullRequiredHours_IsBestEffort()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateDemandCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = h.RoleId,
            RequiredHours = null
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.DemandChanges.Single().RequiredHours.Should().BeNull();
    }

    [Fact]
    public async Task CreateDemand_DryRun_CommitsNothing()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateDemandCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = h.RoleId,
            RequiredHours = TimeSpan.FromHours(40),
            DryRun = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Committed.Should().BeFalse();
        result.Value.DemandChanges.Should().ContainSingle();
        h.DemandCount().Should().Be(1); // baseline only; nothing committed
        h.Db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDemand_UnknownRole_Validation()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new CreateDemandCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = Guid.NewGuid(),
            RequiredHours = TimeSpan.FromHours(40)
        });

        result.IsSuccess.Should().BeFalse();
        h.DemandCount().Should().Be(1); // baseline only
    }

    [Fact]
    public async Task CreateDemand_ClosedProject_Conflict()
    {
        var h = PlanCommandHarness.Create();
        // Drive the seeded root to Closed via the aggregate, persist directly.
        var node = h.Db.ProjectNodes.Single(p => p.Id == h.ProjectNodeId);
        node.Start();
        node.Complete();
        h.Db.SaveChanges();
        h.Db.ChangeTracker.Clear();

        var result = await h.Service.ExecuteAsync(new CreateDemandCommand
        {
            ProjectNodeId = h.ProjectNodeId,
            RoleId = h.RoleId,
            RequiredHours = TimeSpan.FromHours(40)
        });

        result.IsSuccess.Should().BeFalse();
        h.DemandCount().Should().Be(1); // baseline only; not created
    }

    // ── editDemand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EditDemand_ChangesRequiredHoursAndOwner()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedDemand(TimeSpan.FromHours(40));

        var result = await h.Service.ExecuteAsync(new EditDemandCommand
        {
            Id = id,
            RequiredHours = TimeSpan.FromHours(60),
            RequiredHoursSet = true,
            OwnerResourceId = h.ResourceId,
            OwnerResourceIdSet = true
        });

        result.IsSuccess.Should().BeTrue();
        var d = h.ReloadDemand(id);
        d.RequiredHours.Should().Be(TimeSpan.FromHours(60));
        d.OwnerResourceId.Should().Be(h.ResourceId);
    }

    [Fact]
    public async Task EditDemand_ClearRequiredHours_BestEffort()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedDemand(TimeSpan.FromHours(40));

        var result = await h.Service.ExecuteAsync(new EditDemandCommand
        {
            Id = id,
            RequiredHours = null,
            RequiredHoursSet = true
        });

        result.IsSuccess.Should().BeTrue();
        h.ReloadDemand(id).RequiredHours.Should().BeNull();
    }

    [Fact]
    public async Task EditDemand_RoleCorrection_ChangesRole_C2()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedDemand(TimeSpan.FromHours(40));
        var newRoleId = h.SeedRole("Senior PM");

        var result = await h.Service.ExecuteAsync(new EditDemandCommand { Id = id, RoleId = newRoleId });

        result.IsSuccess.Should().BeTrue();
        h.ReloadDemand(id).RoleId.Should().Be(newRoleId);
    }

    [Fact]
    public async Task EditDemand_UnknownDemand_NotFound()
    {
        var h = PlanCommandHarness.Create();

        var result = await h.Service.ExecuteAsync(new EditDemandCommand
        {
            Id = Guid.NewGuid(),
            RequiredHours = TimeSpan.FromHours(10),
            RequiredHoursSet = true
        });

        result.IsSuccess.Should().BeFalse();
    }

    // ── deleteDemand ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDemand_RemovesDemand()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedDemand(TimeSpan.FromHours(40));

        var result = await h.Service.ExecuteAsync(new DeleteDemandCommand { Id = id });

        result.IsSuccess.Should().BeTrue();
        result.Value.DemandChanges.Single().Kind.Should().Be(PlanChangeKind.Deleted);
        h.DemandCount().Should().Be(1); // baseline remains; the seeded one is gone
    }

    [Fact]
    public async Task DeleteDemand_DryRun_CommitsNothing()
    {
        var h = PlanCommandHarness.Create();
        var id = h.SeedDemand(TimeSpan.FromHours(40));

        var result = await h.Service.ExecuteAsync(new DeleteDemandCommand { Id = id, DryRun = true });

        result.IsSuccess.Should().BeTrue();
        result.Value.Committed.Should().BeFalse();
        h.DemandCount().Should().Be(2); // baseline + seeded; nothing deleted
    }
}
