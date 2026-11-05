using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;
using TicTacToe.Domain.Players;

namespace TicTacToe.Application.Games;

/// <summary>Assembles GameDto by resolving player usernames for a (possibly uncommitted) aggregate.</summary>
public sealed class GameDtoLoader(IPlayerProfileRepository profileRepository)
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
}
