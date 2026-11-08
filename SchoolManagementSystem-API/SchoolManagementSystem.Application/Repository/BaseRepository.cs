using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public abstract class BaseRepository(SchoolManagementSystemDbContext dbContext)
{
    protected readonly SchoolManagementSystemDbContext Db = dbContext;

    public void Add<T>(T entity) where T : class => Db.Set<T>().Add(entity);

    public void AddRange<T>(IEnumerable<T> entities) where T : class => Db.Set<T>().AddRange(entities);

    public void Remove<T>(T entity) where T : class => Db.Set<T>().Remove(entity);

    public void RemoveRange<T>(IEnumerable<T> entities) where T : class => Db.Set<T>().RemoveRange(entities);

    public async Task<T?> FindAsync<T>(int id, CancellationToken cancellationToken = default) where T : class =>
        await Db.Set<T>().FindAsync([id], cancellationToken);
}
