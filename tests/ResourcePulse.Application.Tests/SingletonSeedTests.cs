using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Application.Tests;

// Get-or-seed for the per-tenant singletons is a READ-THEN-INSERT, i.e. a race —
// and the per-tenant unique index turns the loser into a 23505 that kills the
// whole unit of work. Two seeders is the normal case since ADR-0032: the API
// seeds on the first /api/config/* read, the signal worker seeds all five on
// every sweep of every tenant.
//
// The InMemory provider does not enforce unique indexes, so the collision is
// injected at the repository seam rather than hoped for from the database.
public class SingletonSeedTests
{
    private static ResourcePulseDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    // The other seeder is a DIFFERENT context on the same database — writing it
    // through ours would save our own pending insert alongside it and stop
    // simulating anything.
    private static void SeedFromAnotherContext(string name, TimeFenceConfiguration winner)
    {
        using var other = NewDb(name);
        other.TimeFenceConfigurations.Add(winner);
        other.SaveChanges();
    }

    // Saves normally, except that the first save throws the duplicate-key error
    // Postgres raises when another connection has just inserted the same
    // singleton.
    private sealed class RacingRepository<T>(ResourcePulseDbContext db, Action onFirstSave)
        : IRepository<T, Guid> where T : Entity<Guid>
    {
        private bool _raced;

        public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct)!;

        public IQueryable<T> Query() => db.Set<T>();

        public async Task AddAsync(T entity, CancellationToken ct = default) =>
            await db.Set<T>().AddAsync(entity, ct);

        public void Remove(T entity) => db.Set<T>().Remove(entity);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (!_raced)
            {
                _raced = true;
                onFirstSave();
                throw new DbUpdateException(
                    "An error occurred while saving.",
                    new InvalidOperationException(
                        "23505: duplicate key value violates unique constraint \"ux_time_fence_configurations_tenant\""));
            }
            return db.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task ALostRaceYieldsTheWinnersRow_InsteadOfThrowing()
    {
        var name = $"seed-{Guid.NewGuid()}";
        var db = NewDb(name);
        var winner = TimeFenceConfiguration.Create(
            Guid.NewGuid(), Duration.Of(3, DurationUnit.Weeks), Duration.Of(4, DurationUnit.Months));

        // The other seeder lands its row exactly between our read and our insert.
        var repository = new RacingRepository<TimeFenceConfiguration>(db, onFirstSave: () => SeedFromAnotherContext(name, winner));

        var result = await SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.TimeFenceConfigurations.AsNoTracking().FirstOrDefaultAsync(token),
            TimeFenceConfiguration.CreateDefault,
            CancellationToken.None);

        result.Id.Should().Be(winner.Id);
        result.FrozenHorizon.Value.Should().Be(3);
    }

    [Fact]
    public async Task TheLoserIsDetached_SoALaterSaveDoesNotRetryTheDoomedInsert()
    {
        // The consequence that actually hurts: a failed SaveChanges leaves the
        // Added entity in the change tracker, so the NEXT save in the same scope
        // replays the same insert — turning one lost race into the failure of
        // whatever unrelated work came after it. In the sweep, that is the entire
        // reconciliation.
        var name = $"seed-{Guid.NewGuid()}";
        var db = NewDb(name);
        var winner = TimeFenceConfiguration.CreateDefault();

        var repository = new RacingRepository<TimeFenceConfiguration>(db, onFirstSave: () => SeedFromAnotherContext(name, winner));

        await SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.TimeFenceConfigurations.AsNoTracking().FirstOrDefaultAsync(token),
            TimeFenceConfiguration.CreateDefault,
            CancellationToken.None);

        db.ChangeTracker.Entries<TimeFenceConfiguration>()
            .Where(e => e.State == EntityState.Added)
            .Should().BeEmpty();

        // And an unrelated later save goes through.
        var act = async () => await db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
        db.TimeFenceConfigurations.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnUncontestedSeedStillJustSeeds()
    {
        var name = $"seed-{Guid.NewGuid()}";
        var db = NewDb(name);
        var repository = new Repository<TimeFenceConfiguration, Guid>(db);

        var result = await SingletonSeed.GetOrSeedAsync(
            db, repository,
            token => db.TimeFenceConfigurations.FirstOrDefaultAsync(token),
            TimeFenceConfiguration.CreateDefault,
            CancellationToken.None);

        result.Should().NotBeNull();
        db.TimeFenceConfigurations.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnExistingRowIsReturnedWithoutInserting()
    {
        var name = $"seed-{Guid.NewGuid()}";
        var db = NewDb(name);
        var existing = TimeFenceConfiguration.CreateDefault();
        db.TimeFenceConfigurations.Add(existing);
        await db.SaveChangesAsync();

        var result = await SingletonSeed.GetOrSeedAsync(
            db, new Repository<TimeFenceConfiguration, Guid>(db),
            token => db.TimeFenceConfigurations.FirstOrDefaultAsync(token),
            TimeFenceConfiguration.CreateDefault,
            CancellationToken.None);

        result.Id.Should().Be(existing.Id);
        db.TimeFenceConfigurations.Should().HaveCount(1);
    }
}
