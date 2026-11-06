using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using TicTacToe.Domain.Contracts;

namespace TicTacToe.Infrastructure.Data;

public abstract class Repository<TAggregateRoot, TKey> : IRepository<TAggregateRoot, TKey>, IDisposable
    where TAggregateRoot : class, IAggregateRoot<TKey>
    where TKey : IComparable
{
    private readonly DbContext _dbContext;
    private readonly DbSet<TAggregateRoot> _dbSet;

    protected Repository(DbContext context)
    {
        _dbContext = context;
        _dbSet = _dbContext.Set<TAggregateRoot>();
    }

    public virtual void Add(TAggregateRoot entity)
    {
        _dbSet.Add(entity);
    }

    public virtual async Task AddAsync(TAggregateRoot entity, CancellationToken cancellationToken)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public virtual void Edit(TAggregateRoot entityToUpdate)
    {
        _dbSet.Attach(entityToUpdate);
        _dbContext.Entry(entityToUpdate).State = EntityState.Modified;
    }

    public virtual Task EditAsync(TAggregateRoot entityToUpdate, CancellationToken cancellationToken)
    {
        Edit(entityToUpdate);
        return Task.CompletedTask;
    }

    public virtual IList<TAggregateRoot> GetAll()
    {
        return [.. _dbSet.AsNoTracking()];
    }

    public virtual async Task<IList<TAggregateRoot>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual TAggregateRoot GetById(TKey id)
    {
        return _dbSet.Find(id)!;
    }

    public virtual async Task<TAggregateRoot?> GetByIdAsync(TKey id, CancellationToken cancellationToken)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public virtual int GetCount(Expression<Func<TAggregateRoot, bool>>? filter = null)
    {
        return filter is null ? _dbSet.Count() : _dbSet.Count(filter);
    }

    public virtual Task<int> GetCountAsync(Expression<Func<TAggregateRoot, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        return filter is null ? _dbSet.CountAsync(cancellationToken) : _dbSet.CountAsync(filter, cancellationToken);
    }

    public virtual void Remove(Expression<Func<TAggregateRoot, bool>> filter)
    {
        var entities = _dbSet.Where(filter).ToList();
        _dbSet.RemoveRange(entities);
    }

    public virtual void Remove(TAggregateRoot entityToDelete)
    {
        if (_dbContext.Entry(entityToDelete).State == EntityState.Detached)
        {
            _dbSet.Attach(entityToDelete);
        }

        _dbSet.Remove(entityToDelete);
    }

    public virtual void Remove(TKey id)
    {
        var entity = _dbSet.Find(id);
        if (entity is not null)
        {
            Remove(entity);
        }
    }

    public virtual void Update(TAggregateRoot entity)
    {
        _dbSet.Update(entity);
    }

    public virtual Task RemoveAsync(Expression<Func<TAggregateRoot, bool>>? filter, CancellationToken cancellationToken)
    {
        Remove(filter!);
        return Task.CompletedTask;
    }

    public virtual Task RemoveAsync(TAggregateRoot entityToDelete, CancellationToken cancellationToken)
    {
        Remove(entityToDelete);
        return Task.CompletedTask;
    }

    public virtual async Task RemoveAsync(TKey id, CancellationToken cancellationToken)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            Remove(entity);
        }
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
