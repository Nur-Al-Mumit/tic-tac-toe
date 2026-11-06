using Cortex.Mediator.Queries;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Query;

public sealed class GetAllGamesQueryHandler(IReadDbContext readDbContext)
    : IQueryHandler<GetAllGamesQuery, Result<IReadOnlyList<LobbyGameDto>>>
{
    private const int Cap = 200;

    public async Task<Result<IReadOnlyList<LobbyGameDto>>> Handle(GetAllGamesQuery query, CancellationToken cancellationToken)
    {
        var statuses = ParseStatuses(query.Status);

        var games = await (from g in readDbContext.Games
                           where statuses.Contains(g.Status)
                           join px in readDbContext.PlayerProfiles on g.PlayerXId equals px.Id into profilesX
                           from px in profilesX.DefaultIfEmpty()
                           join po in readDbContext.PlayerProfiles on g.PlayerOId!.Value equals po.Id into profilesO
                           from po in profilesO.DefaultIfEmpty()
                           orderby g.CreatedAtUtc descending
                           select new LobbyGameDto(
                               g.Id,
                               g.Status.ToString(),
                               new PlayerRef(g.PlayerXId, px != null ? px.Username : "player"),
                               po != null && g.PlayerOId != null ? new PlayerRef(g.PlayerOId!.Value, po.Username) : null,
                               g.CreatedAtUtc))
            .Take(Cap)
            .ToListAsync(cancellationToken);

        return games;
    }

    private static List<GameStatus> ParseStatuses(string? status)
        => status?.ToLowerInvariant() switch
        {
            null or "" => [GameStatus.Waiting, GameStatus.InProgress],
            "waiting" => [GameStatus.Waiting],
            "inprogress" => [GameStatus.InProgress],
            _ => [GameStatus.Waiting, GameStatus.InProgress],
        };
}
