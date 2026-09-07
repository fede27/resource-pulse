using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Shared;

/// <summary>
/// Get-or-seed for the per-tenant singletons (the org-level configuration
/// aggregates of ADR-0020/0032, and the detector's sweep state).
/// </summary>
/// <remarks>
/// <para>
/// Read-then-insert is a RACE, and under multi-tenancy the singletons are
/// protected by a unique index on <c>tenant_id</c> — so the loser of that race
/// does not get a duplicate row, it gets a <c>23505</c> and the whole unit of
/// work dies. Two readers seeing "no row yet" at the same instant is not exotic:
/// the API seeds on the first read of <c>/api/config/*</c>, and since ADR-0032
/// the signal worker seeds all five on every sweep of every tenant.
/// </para>
/// <para>
/// The loser must ALSO be detached. A failed <c>SaveChanges</c> leaves the Added
/// entity in the change tracker, so the next save in the same scope retries the
/// same doomed insert — which turns one lost race into a failure of whatever
/// unrelated work came after it.
/// </para>
/// </remarks>
public static class SingletonSeed
{
    public static async Task<T> GetOrSeedAsync<T>(
        ResourcePulseDbContext db,
        IRepository<T, Guid> repository,
        Func<CancellationToken, Task<T?>> read,
        Func<T> createDefault,
        CancellationToken ct)
        where T : Entity<Guid>
    {
        var existing = await read(ct);
        if (existing is not null) return existing;

        var seeded = createDefault();
        try
        {
            await repository.AddAsync(seeded, ct);
            await repository.SaveChangesAsync(ct);
            return seeded;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Somebody seeded it between our read and our insert. Their row is
            // just as good as ours would have been — these are defaults, not a
            // decision anybody made.
            db.Entry(seeded).State = EntityState.Detached;

            return await read(ct) ?? throw new InvalidOperationException(
                $"Seeding {typeof(T).Name} lost a race, but the winning row is not readable. " +
                "This usually means the tenant scope changed between the two reads.",
                ex);
        }
    }

    // Message-based, matching the house convention (see TeamService): the
    // provider-specific error code is not surfaced by DbUpdateException itself.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
