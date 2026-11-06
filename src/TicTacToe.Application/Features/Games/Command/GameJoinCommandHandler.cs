using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Common;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using Microsoft.Extensions.Options;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>
/// Race-safe join (US-07): two simultaneous joiners load the same Waiting game; the loser of the
/// xmin save detaches, reloads once, re-evaluates the domain rules on fresh state and gets Games.Full.
/// </summary>
public sealed class GameJoinCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    GameDtoLoader dtoLoader,
    IOptions<GameRulesOptions> gameRules) : ICommandHandler<GameJoinCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GameJoinCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } joinerId)
        {
            return Result.Failure<GameDto>(Error.Unauthorized());
        }

        Game? game = null;
        for (var attempt = 0; ; attempt++)
        {
            game = await unitOfWork.GameRepository.GetGameByIdAsync(command.GameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure<GameDto>(new Error("Games.NotFound", "Game not found."));
            }

            if (game.Status != GameStatus.Waiting)
            {
                return Result.Failure<GameDto>(new Error("Games.Full", "Game is already full."));
            }

            if (game.PlayerXId == joinerId)
            {
                return Result.Failure<GameDto>(new Error("Games.CannotJoinOwnGame", "You cannot join your own game."));
            }

            var activeGame = await unitOfWork.GameRepository.GetActiveGameForPlayerAsync(joinerId, cancellationToken);
            if (activeGame is not null && activeGame.Id != command.GameId)
            {
                return Result.Failure<GameDto>(new Error("Games.Busy", "You already have an active game."));
            }

            try
            {
                game.Join(joinerId, dateTimeProvider.UtcNow, gameRules.Value.MoveTimeout);
                await unitOfWork.SaveAsync();
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                unitOfWork.GameRepository.Detach(game); // drop the stale tracked copy so the next load is fresh
            }
        }

        return await dtoLoader.LoadAsync(game!, cancellationToken);
    }
}
