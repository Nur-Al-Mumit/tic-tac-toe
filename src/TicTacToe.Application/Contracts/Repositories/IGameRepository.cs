using TicTacToe.Domain.Contracts;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Contracts.Repositories;

public interface IGameRepository : IRepository<Game, Guid>
{
    /// <summary>Loads the aggregate with its moves, tracked (for the write side).</summary>
    Task<Game?> GetGameByIdAsync(Guid gameId, CancellationToken cancellationToken);

    Task<Game?> GetActiveGameForPlayerAsync(Guid playerId, CancellationToken cancellationToken);

    /// <summary>Detaches a stale tracked aggregate so a reload-once retry reads fresh database state.</summary>
    void Detach(Game game);

    /// <summary>InProgress games whose move deadline has passed (the timeout sweeper's input).</summary>
    Task<IReadOnlyList<Game>> GetTimedOutGamesAsync(DateTime utcNow, CancellationToken cancellationToken);
}
