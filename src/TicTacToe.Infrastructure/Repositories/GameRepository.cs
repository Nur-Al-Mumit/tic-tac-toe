using Microsoft.EntityFrameworkCore;
using TicTacToe.Domain.Games;
using TicTacToe.Infrastructure.Persistence;

namespace TicTacToe.Infrastructure.Repositories;

public class GameRepository(AppDbContext context) : IGameRepository
{
    public Task<Game?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken = default)
        => context.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

    public Task<Game?> GetActiveGameForPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
        => context.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(
                g => (g.PlayerXId == playerId || g.PlayerOId == playerId)
                    && (g.Status == GameStatus.Waiting || g.Status == GameStatus.InProgress),
                cancellationToken);

    public async Task AddAsync(Game game, CancellationToken cancellationToken = default)
        => await context.Games.AddAsync(game, cancellationToken);

    public void Detach(Game game)
    {
        context.Entry(game).State = EntityState.Detached;
        foreach (var move in game.Moves)
        {
            context.Entry(move).State = EntityState.Detached;
        }
    }
}
