using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Domain.Entities.GameAggregate;
using TicTacToe.Infrastructure.Data;

namespace TicTacToe.Infrastructure.Data.Repositories;

public class GameRepository(ApplicationDbContext context) : Repository<Game, Guid>(context), IGameRepository
{
    public Task<Game?> GetGameByIdAsync(Guid gameId, CancellationToken cancellationToken)
        => context.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

    public Task<Game?> GetActiveGameForPlayerAsync(Guid playerId, CancellationToken cancellationToken)
        => context.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(
                g => (g.PlayerXId == playerId || g.PlayerOId == playerId)
                    && (g.Status == GameStatus.Waiting || g.Status == GameStatus.InProgress),
                cancellationToken);

    public void Detach(Game game)
    {
        context.Entry(game).State = EntityState.Detached;
        foreach (var move in game.Moves)
        {
            context.Entry(move).State = EntityState.Detached;
        }
    }
}
