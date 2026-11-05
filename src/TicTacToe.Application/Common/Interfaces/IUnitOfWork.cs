using TicTacToe.Domain.Common;

namespace TicTacToe.Application.Common.Interfaces;

/// <summary>The single commit point: saves changes, then dispatches domain events post-commit.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    IReadOnlyCollection<IDomainEvent> DequeueDomainEvents();
}
