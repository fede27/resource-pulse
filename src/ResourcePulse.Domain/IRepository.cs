namespace ResourcePulse.Domain;

public interface IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : IEquatable<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    IQueryable<TEntity> Query();
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
