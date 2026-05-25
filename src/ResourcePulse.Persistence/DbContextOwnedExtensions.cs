using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ResourcePulse.Persistence;

public static class DbContextOwnedExtensions
{
    /// <summary>
    /// Force-marks a newly-added owned child as <see cref="EntityState.Added"/>
    /// in the change tracker. Use immediately after appending the child to its
    /// owner's <c>OwnsMany</c> collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core's navigation-discovery on a tracked <c>OwnsMany</c> parent treats
    /// any new entity with a non-default primary key as "existing being
    /// attached" and tracks it as <see cref="EntityState.Modified"/> — which
    /// produces a stray <c>UPDATE … WHERE id = newKey</c> that affects 0 rows
    /// and surfaces as a <c>DbUpdateConcurrencyException</c> at SaveChanges.
    /// </para>
    /// <para>
    /// Our domain factories assign PKs at construction (<see cref="Guid.NewGuid"/>
    /// or natural composite keys), so the PK is always non-default. This
    /// helper overrides the heuristic.
    /// </para>
    /// <para>
    /// Owned types are shared-type entities — calling <c>db.Entry(child)</c>
    /// directly throws because EF cannot disambiguate the owner. The supported
    /// API is to walk the entry through the owner's collection navigation.
    /// </para>
    /// </remarks>
    public static void MarkOwnedAdded<TOwner, TOwned>(
        this DbContext db,
        TOwner owner,
        Expression<Func<TOwner, IEnumerable<TOwned>>> collection,
        TOwned child)
        where TOwner : class
        where TOwned : class
    {
        var entry = db.Entry(owner).Collection(collection).FindEntry(child);
        if (entry is null)
            throw new InvalidOperationException(
                $"Owned {typeof(TOwned).Name} is not tracked under the supplied owner. " +
                "Append the child to the owner's collection before calling MarkOwnedAdded.");
        entry.State = EntityState.Added;
    }
}
