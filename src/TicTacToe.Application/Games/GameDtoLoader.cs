using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;
using TicTacToe.Domain.Players;

namespace TicTacToe.Application.Games;

/// <summary>Assembles GameDto for a (possibly uncommitted) aggregate, or from a read-side reload.</summary>
public sealed class GameDtoLoader(IReadDbContext readDbContext, IPlayerProfileRepository profileRepository)
{
    public async Task<GameDto> LoadAsync(Game game, CancellationToken cancellationToken)
    {
        var ids = new List<Guid> { game.PlayerXId };
        if (game.PlayerOId is { } o)
        {
            ids.Add(o);
        }

        var profiles = await profileRepository.GetByUserIdsAsync(ids, cancellationToken);
        var names = profiles.ToDictionary(p => p.Id, p => p.Username);

        return game.ToGameDto(
            names.GetValueOrDefault(game.PlayerXId, "player"),
            game.PlayerOId is { } oid ? names.GetValueOrDefault(oid, "player") : null);
    }

    /// <summary>Read-side load (no tracking, moves included for the board); null when the game does not exist.</summary>
    public async Task<GameDto?> LoadByIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await readDbContext.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        return game is null ? null : await LoadAsync(game, cancellationToken);
    }
}
