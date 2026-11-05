using Microsoft.EntityFrameworkCore;
using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;

namespace TicTacToe.Application.Games.Queries;

/// <summary>status filter: "waiting", "inprogress" or both (V1 lobby list uses waiting only).</summary>
public sealed record GetLobbyGamesQuery(string? Status = null) : IQuery<Result<IReadOnlyList<LobbyGameDto>>>;

public sealed class GetLobbyGamesQueryHandler(IReadDbContext readDbContext)
    : IQueryHandler<GetLobbyGamesQuery, Result<IReadOnlyList<LobbyGameDto>>>
{
    private const int Cap = 200;

    public async Task<Result<IReadOnlyList<LobbyGameDto>>> Handle(GetLobbyGamesQuery query, CancellationToken cancellationToken)
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

    private static List<Domain.Games.GameStatus> ParseStatuses(string? status)
        => (status?.ToLowerInvariant()) switch
        {
            null or "" => [Domain.Games.GameStatus.Waiting, Domain.Games.GameStatus.InProgress],
            "waiting" => [Domain.Games.GameStatus.Waiting],
            "inprogress" => [Domain.Games.GameStatus.InProgress],
            _ => [Domain.Games.GameStatus.Waiting, Domain.Games.GameStatus.InProgress],
        };
}
