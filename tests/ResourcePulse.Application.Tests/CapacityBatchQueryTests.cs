using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;

namespace ResourcePulse.Application.Tests;

// Batch capacity read (P1 of api-roundtrip-consolidation.md): one round of
// queries for the population, then the pure CapacityCalculator per resource.
// The key guarantee is PARITY with the singular read — same calendar, closures
// and adjustments semantics, just amortized loading.
public class CapacityBatchQueryTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Sun = new(2026, 6, 7);
    private static readonly DateOnly Wed = new(2026, 6, 3);
    private static readonly DateOnly Tue = new(2026, 6, 2);

    private sealed record Fixture(
        LiveCapacityQueryService Svc,
        Guid TizioId,
        Guid CaioId,
        Guid InactiveId);

    // Shared Mon–Fri 9→17 calendar; Wednesday company closure; Caio has a
    // full-day absence on Tuesday; a third resource is deactivated.
    private static Fixture Seed()
    {
        var options = new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase($"capbatch-{Guid.NewGuid()}")
            .Options;
        var db = new ResourcePulseDbContext(options);

        var calendar = BusinessCalendar.Create("Standard", isDefault: true);
        foreach (var dow in new[]
                 {
                     DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                     DayOfWeek.Thursday, DayOfWeek.Friday
                 })
        {
            calendar.AddWorkWindow(WorkWindow.Create(
                dow, new TimeOnly(9, 0), new TimeOnly(17, 0), new DateOnly(2020, 1, 1), null));
        }

        var tizio = Resource.Create("Tizio", calendar.Id);
        var caio = Resource.Create("Caio", calendar.Id);
        caio.AddAdjustment(IndividualAdjustment.Create(
            Tue, Tue, AdjustmentType.Absence, hours: null, reason: "Ferie", notes: null));
        var inactive = Resource.Create("Sempronio", calendar.Id);
        inactive.Deactivate();

        db.Set<BusinessCalendar>().Add(calendar);
        db.Resources.AddRange(tizio, caio, inactive);
        db.Set<CompanyClosure>().Add(CompanyClosure.Create(Wed, Wed, "Chiusura aziendale"));
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return new Fixture(new LiveCapacityQueryService(db), tizio.Id, caio.Id, inactive.Id);
    }

    [Fact]
    public async Task Batch_MatchesTheSingularReadPerResource()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync([f.TizioId, f.CaioId], Mon, Sun);
        var tizioSingle = await f.Svc.GetForResourceAsync(f.TizioId, Mon, Sun);
        var caioSingle = await f.Svc.GetForResourceAsync(f.CaioId, Mon, Sun);

        batch.IsSuccess.Should().BeTrue();
        batch.Value.Should().HaveCount(2);
        batch.Value[f.TizioId].Should().BeEquivalentTo(tizioSingle.Value, o => o.WithStrictOrdering());
        batch.Value[f.CaioId].Should().BeEquivalentTo(caioSingle.Value, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Batch_AppliesClosuresAndAdjustments()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync([f.TizioId, f.CaioId], Mon, Sun);

        var tizio = batch.Value[f.TizioId].ToDictionary(d => d.Date, d => d.Hours);
        tizio[Mon].Should().Be(TimeSpan.FromHours(8));
        tizio[Wed].Should().Be(TimeSpan.Zero); // company closure hits everyone
        tizio[Sun].Should().Be(TimeSpan.Zero); // no weekend window

        var caio = batch.Value[f.CaioId].ToDictionary(d => d.Date, d => d.Hours);
        caio[Tue].Should().Be(TimeSpan.Zero); // full-day absence
        caio[Mon].Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public async Task NullIds_ReturnsActiveResourcesOnly()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync(null, Mon, Sun);

        batch.IsSuccess.Should().BeTrue();
        batch.Value.Keys.Should().BeEquivalentTo([f.TizioId, f.CaioId]);
    }

    [Fact]
    public async Task ExplicitIds_IncludeInactiveResources()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync([f.InactiveId], Mon, Sun);

        batch.Value.Should().ContainKey(f.InactiveId);
    }

    [Fact]
    public async Task UnknownIds_AreSimplyAbsent()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync([f.TizioId, Guid.NewGuid()], Mon, Sun);

        batch.IsSuccess.Should().BeTrue();
        batch.Value.Keys.Should().BeEquivalentTo([f.TizioId]);
    }

    [Fact]
    public async Task InvalidRange_IsRejected()
    {
        var f = Seed();

        var batch = await f.Svc.GetForResourcesAsync([f.TizioId], Sun, Mon);

        batch.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Segments_CompressTheWeekAroundClosureAndWeekend()
    {
        var f = Seed();

        var result = await f.Svc.GetSegmentsForResourcesAsync([f.TizioId], Mon, Sun);

        result.IsSuccess.Should().BeTrue();
        var tizio = result.Value.Single(x => x.ResourceId == f.TizioId);
        // Mon–Tue 8h · Wed closure gap · Thu–Fri 8h · weekend gap.
        tizio.Segments.Should().HaveCount(2);
        tizio.Segments[0].Should().BeEquivalentTo(
            new CapacitySegmentDto { From = Mon, To = Tue, HoursPerDay = TimeSpan.FromHours(8) });
        tizio.Segments[1].Should().BeEquivalentTo(
            new CapacitySegmentDto { From = Wed.AddDays(1), To = Wed.AddDays(2), HoursPerDay = TimeSpan.FromHours(8) });
    }
}
