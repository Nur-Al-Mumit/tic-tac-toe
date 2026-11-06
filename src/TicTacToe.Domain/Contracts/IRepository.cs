using System.Linq.Expressions;

namespace TicTacToe.Domain.Contracts;

public interface IRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>
    where TKey : IComparable
{
    void Add(TAggregateRoot entity);

    Task AddAsync(TAggregateRoot entity, CancellationToken cancellationToken);

    void Edit(TAggregateRoot entityToUpdate);

    Task EditAsync(TAggregateRoot entityToUpdate, CancellationToken cancellationToken);

    IList<TAggregateRoot> GetAll();

    Task<IList<TAggregateRoot>> GetAllAsync(CancellationToken cancellationToken);

    TAggregateRoot GetById(TKey id);

    Task<TAggregateRoot?> GetByIdAsync(TKey id, CancellationToken cancellationToken);

    int GetCount(Expression<Func<TAggregateRoot, bool>>? filter = null);

    Task<int> GetCountAsync(Expression<Func<TAggregateRoot, bool>>? filter = null, CancellationToken cancellationToken = default);

    void Remove(Expression<Func<TAggregateRoot, bool>> filter);

    void Remove(TAggregateRoot entityToDelete);

    void Remove(TKey id);

    void Update(TAggregateRoot entity);

    Task RemoveAsync(Expression<Func<TAggregateRoot, bool>>? filter, CancellationToken cancellationToken);

    Task RemoveAsync(TAggregateRoot entityToDelete, CancellationToken cancellationToken);

    Task RemoveAsync(TKey id, CancellationToken cancellationToken);
}
