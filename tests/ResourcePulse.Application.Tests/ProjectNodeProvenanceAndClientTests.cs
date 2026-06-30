using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Projects;

namespace ResourcePulse.Application.Tests;

// Service-level behaviour of the data-model evolution (project-gap.md ★):
//   M1 — `Client` round-trips through create / read / update-project, and is
//        project-only.
//   M3 — `IsProposed` is DERIVED in ToDtoWithMetrics from CommitmentPolicy
//        (the complement of the hard-commit threshold, ADR-0020), not stored.
// Uses the EF Core InMemory provider (same rationale as PlanCommandHarness).
public class ProjectNodeProvenanceAndClientTests
{
    private static (ProjectNodeService svc, ResourcePulseDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"pn-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var config = new TypeAdapterConfig();
        new ProjectNodeMappingRegister().Register(config);
        var mapper = new Mapper(config);

        var policy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db));

        var svc = new ProjectNodeService(
            new Repository<ProjectNode, Guid>(db), db, mapper, policy);
        return (svc, db);
    }

    private static CreateProjectNodeDto NewProject(
        CommitmentLevel commitment, string? client = null) => new()
    {
        NodeType = ProjectNodeType.Project,
        Name = "Apollo",
        Type = ProjectType.Customer,
        CommitmentLevel = commitment,
        Client = client
    };

    // ── M1: Client ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsClient_AndReadReturnsIt()
    {
        var (svc, _) = Build();

        var created = await svc.CreateAsync(NewProject(CommitmentLevel.Committed, "ACME S.p.A."));

        created.IsSuccess.Should().BeTrue();
        created.Value.Client.Should().Be("ACME S.p.A.");

        var read = await svc.GetByIdAsync(created.Value.Id);
        read.Value.Client.Should().Be("ACME S.p.A.");
    }

    [Fact]
    public async Task UpdateProject_ChangesClient()
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(NewProject(CommitmentLevel.Committed, "ACME"));

        var updated = await svc.UpdateProjectAsync(created.Value.Id, new UpdateProjectDto
        {
            Type = ProjectType.Customer,
            CommitmentLevel = CommitmentLevel.Committed,
            Client = "Globex"
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Client.Should().Be("Globex");
    }

    [Fact]
    public async Task Phase_NeverCarriesClient()
    {
        var (svc, _) = Build();
        var root = await svc.CreateAsync(NewProject(CommitmentLevel.Planned, "ACME"));

        var phase = await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Phase,
            ParentId = root.Value.Id,
            Name = "Phase 1",
            // Client is ignored on non-root nodes (CreateChild doesn't take it).
            Client = "ShouldBeIgnored"
        });

        phase.IsSuccess.Should().BeTrue();
        phase.Value.Client.Should().BeNull();
    }

    // ── M3: IsProposed (provenance) ─────────────────────────────────────────────

    [Theory]
    [InlineData(CommitmentLevel.Exploratory, true)]
    [InlineData(CommitmentLevel.Planned, true)]
    [InlineData(CommitmentLevel.Committed, false)]
    [InlineData(CommitmentLevel.Critical, false)]
    public async Task IsProposed_IsComplementOfDefaultHardCommitThreshold(
        CommitmentLevel level, bool expectedProposed)
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(NewProject(level));

        var read = await svc.GetByIdAsync(created.Value.Id);

        read.Value.IsProposed.Should().Be(expectedProposed);
    }

    [Fact]
    public async Task IsProposed_FollowsConfiguredThreshold_NotALiteral()
    {
        var (svc, db) = Build();
        var policy = new CommitmentPolicyService(
            new Repository<CommitmentPolicyConfiguration, Guid>(db));

        var created = await svc.CreateAsync(NewProject(CommitmentLevel.Committed));

        // Default {Committed, Critical}: Committed is NOT proposed.
        (await svc.GetByIdAsync(created.Value.Id)).Value.IsProposed.Should().BeFalse();

        // Narrow the threshold to {Critical}: now Committed is "proposed".
        await policy.UpdateAsync(new UpdateCommitmentPolicyDto
        {
            HardCommitLevels = [CommitmentLevel.Critical]
        });

        (await svc.GetByIdAsync(created.Value.Id)).Value.IsProposed.Should().BeTrue();
    }

    [Fact]
    public async Task IsProposed_IsNull_OnNonProjectNodes()
    {
        var (svc, _) = Build();
        var root = await svc.CreateAsync(NewProject(CommitmentLevel.Committed));

        var phase = await svc.CreateAsync(new CreateProjectNodeDto
        {
            NodeType = ProjectNodeType.Phase,
            ParentId = root.Value.Id,
            Name = "Phase 1"
        });

        phase.Value.IsProposed.Should().BeNull();
    }
}
