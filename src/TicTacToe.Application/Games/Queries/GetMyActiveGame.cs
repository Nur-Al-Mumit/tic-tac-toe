using Cortex.Mediator.Queries;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Queries;

/// <summary>US-13: the resync source after a page refresh or hub reconnect — returns null when the player has no active game (→ 204).</summary>
public sealed record GetMyActiveGameQuery : IQuery<Result<GameDto?>>;

public sealed class GetMyActiveGameQueryHandler(IReadDbContext readDbContext, GameDtoLoader dtoLoader, ICurrentUser currentUser)
    : IQueryHandler<GetMyActiveGameQuery, Result<GameDto?>>
{
    public async Task<Result<GameDto?>> Handle(GetMyActiveGameQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } currentId)
        {
            return Result.Failure<GameDto?>(Error.Unauthorized());
        }

        var active = await readDbContext.Games
            .Where(g => g.Status == GameStatus.Waiting || g.Status == GameStatus.InProgress)
            .Where(g => g.PlayerXId == currentId || g.PlayerOId == currentId)
            .OrderByDescending(g => g.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return active is null ? Result.Success<GameDto?>(null) : await dtoLoader.LoadAsync(active, cancellationToken);
    }
}
