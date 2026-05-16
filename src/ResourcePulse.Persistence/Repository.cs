using Microsoft.EntityFrameworkCore;
using ResourcePulse.Domain;

namespace ResourcePulse.Persistence;

public class Repository<TEntity, TId>(ResourcePulseDbContext context)
    : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : IEquatable<TId>
{
    // GetByIdAsync uses tracking so callers can mutate and save
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await context.Set<TEntity>().FindAsync(new object?[] { id }, ct);

    // Query uses AsNoTracking for read-only paths and DevExtreme projection
    public IQueryable<TEntity> Query() =>
        context.Set<TEntity>().AsNoTracking();

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await context.Set<TEntity>().AddAsync(entity, ct);

    public void Remove(TEntity entity) =>
        context.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
