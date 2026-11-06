using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Players.Dtos;

namespace TicTacToe.Application.Features.Players.Query;

public sealed class GetOnlinePlayersQueryHandler(
    IPresenceTracker presenceTracker,
    IPlayerProfileRepository profileRepository,
    IGameRepository gameRepository)
    : IQueryHandler<GetOnlinePlayersQuery, Result<IReadOnlyList<OnlinePlayerDto>>>
{
    public async Task<Result<IReadOnlyList<OnlinePlayerDto>>> Handle(GetOnlinePlayersQuery query, CancellationToken cancellationToken)
    {
        var onlineIds = presenceTracker.GetOnlineUserIds();
        if (onlineIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<OnlinePlayerDto>>([]);
        }

        var profiles = await profileRepository.GetByUserIdsAsync(onlineIds, cancellationToken);
        var names = profiles.ToDictionary(p => p.Id, p => p.Username);

        var activeGames = await gameRepository.GetActiveGamesAsync(cancellationToken);
        var inGame = new HashSet<Guid>();
        foreach (var game in activeGames)
        {
            inGame.Add(game.PlayerXId);
            if (game.PlayerOId is { } o)
            {
                inGame.Add(o);
            }
        }

        return onlineIds
            .Select(id => new OnlinePlayerDto(id, names.GetValueOrDefault(id, "player"), inGame.Contains(id)))
            .ToList();
    }
}
