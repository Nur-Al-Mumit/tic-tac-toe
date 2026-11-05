namespace TicTacToe.Domain.Games;

/// <summary>Repository contract in the Domain; the implementation lives in Infrastructure. No IQueryable escapes (ADR-0001).</summary>
public interface IGameRepository
{
    Task<Game?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<Game?> GetActiveGameForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task AddAsync(Game game, CancellationToken cancellationToken = default);
}
