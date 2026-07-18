using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Load;

namespace ResourcePulse.Application.Tests;

// Batch commitment profiles (consolidation P2): the plural of the ADR-0023 read.
// The key guarantee is PARITY with the singular profile — same segments, same
// per-project decomposition — plus the batch ids semantics shared with the
// capacity batch (null/empty = active population, explicit ids honour inactive,
// unknown ids absent).
public class LoadProfilesBatchTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private sealed record Fixture(
        LiveLoadQueryService Svc,
        Guid TizioId,
        Guid CaioId,
        Guid InactiveId);

    // Tizio: 50% Hard on Alpha all week + 30% Tentative on Beta from Wednesday.
    // Caio: no allocations. Sempronio: deactivated, no allocations.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"profilesbatch-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var tizio = Resource.Create("Tizio", Guid.NewGuid());
        var caio = Resource.Create("Caio", Guid.NewGuid());
        var inactive = Resource.Create("Sempronio", Guid.NewGuid());
        inactive.Deactivate();

        var role = Role.Create("Backend");
        var alpha = ProjectNode.CreateRoot("Alpha", "A", ProjectType.Internal, CommitmentLevel.Committed, null);
        var beta = ProjectNode.CreateRoot("Beta", "B", ProjectType.Internal, CommitmentLevel.Committed, null);
        var dAlpha = Demand.Create(alpha.Id, role.Id, null, DemandProvenance.Declared);
        var dBeta = Demand.Create(beta.Id, role.Id, null, DemandProvenance.Declared);

        var hard = Allocation.CreateCoverage(dAlpha.Id, alpha.Id, tizio.Id, Mon, Fri, 50m, status: AllocationStatus.Hard);
        var tentative = Allocation.CreateCoverage(dBeta.Id, beta.Id, tizio.Id, Mon.AddDays(2), Fri, 30m);

        db.Resources.AddRange(tizio, caio, inactive);
        db.Roles.Add(role);
        db.ProjectNodes.AddRange(alpha, beta);
        db.Demands.AddRange(dAlpha, dBeta);
        db.Allocations.AddRange(hard, tentative);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture(
            new LiveLoadQueryService(db, new FixedCapacity(TimeSpan.FromHours(8))),
            tizio.Id, caio.Id, inactive.Id);
    }

    [Fact]
    public async Task Batch_MatchesTheSingularProfilePerResource()
    {
        var f = Seed();

        var batch = await f.Svc.GetCommitmentProfilesForResourcesAsync([f.TizioId], Mon, Fri);
        var single = await f.Svc.GetCommitmentProfileForResourceAsync(f.TizioId, Mon, Fri);

        batch.IsSuccess.Should().BeTrue();
        var tizio = batch.Value.Single(x => x.ResourceId == f.TizioId);
        tizio.Segments.Should().BeEquivalentTo(single.Value, o => o.WithStrictOrdering());
        // Sanity on the shape itself: 50% until Beta starts, then 80% stacked.
        tizio.Segments.Should().HaveCount(2);
        tizio.Segments[0].Percent.Should().Be(50m);
        tizio.Segments[1].Percent.Should().Be(80m);
        tizio.Segments[1].ByProject.Should().HaveCount(2);
    }

    [Fact]
    public async Task StatusFilter_NarrowsTheBatchLikeTheSingular()
    {
        var f = Seed();

        var hardOnly = await f.Svc.GetCommitmentProfilesForResourcesAsync(
            [f.TizioId], Mon, Fri, AllocationStatus.Hard);

        var tizio = hardOnly.Value.Single(x => x.ResourceId == f.TizioId);
        tizio.Segments.Should().ContainSingle(); // the tentative Beta block is gone
        tizio.Segments[0].Percent.Should().Be(50m);
    }

    [Fact]
    public async Task NullIds_CoverTheActivePopulation_IdleProfileIsOneZeroSegment()
    {
        var f = Seed();

        var batch = await f.Svc.GetCommitmentProfilesForResourcesAsync(null, Mon, Fri);

        batch.Value.Select(x => x.ResourceId).Should().BeEquivalentTo([f.TizioId, f.CaioId]);
        // Parity with the singular read: an idle person is a single 0% run over
        // the whole range (the calculator's shape), not an absent/empty profile.
        var caio = batch.Value.Single(x => x.ResourceId == f.CaioId);
        caio.Segments.Should().ContainSingle();
        caio.Segments[0].Percent.Should().Be(0m);
        caio.Segments[0].From.Should().Be(Mon);
        caio.Segments[0].To.Should().Be(Fri);
    }

    [Fact]
    public async Task ExplicitIds_HonourInactive_AndDropUnknown()
    {
        var f = Seed();

        var batch = await f.Svc.GetCommitmentProfilesForResourcesAsync(
            [f.InactiveId, Guid.NewGuid()], Mon, Fri);

        batch.Value.Select(x => x.ResourceId).Should().BeEquivalentTo([f.InactiveId]);
    }

    [Fact]
    public async Task InvalidOrOversizedRange_IsRejected()
    {
        var f = Seed();

        (await f.Svc.GetCommitmentProfilesForResourcesAsync([f.TizioId], Fri, Mon)).IsFailure.Should().BeTrue();
        (await f.Svc.GetCommitmentProfilesForResourcesAsync([f.TizioId], Mon, Mon.AddDays(400))).IsFailure.Should().BeTrue();
    }
}
