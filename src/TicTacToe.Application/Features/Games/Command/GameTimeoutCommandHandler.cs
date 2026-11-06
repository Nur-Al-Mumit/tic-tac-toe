using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>
/// Ends a game by timeout (GameRules:MoveTimeoutSeconds). The aggregate re-checks the deadline, so a
/// move that landed between the sweeper's scan and this save resolves as a no-op via the xmin reload.
/// </summary>
public sealed class GameTimeoutCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<GameTimeoutCommand, Result>
{
    public async Task<Result> Handle(GameTimeoutCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var game = await unitOfWork.GameRepository.GetGameByIdAsync(command.GameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure(new Error("Games.NotFound", "Game not found."));
            }

            // Not this player's turn any more, or the clock was reset — nothing to do.
            if (game.PlayerIdOfTurn != command.PlayerId
                || game.MoveDeadlineUtc is not { } deadline
                || deadline > dateTimeProvider.UtcNow)
            {
                return Result.Success();
            }

            try
            {
                game.Timeout(command.PlayerId, dateTimeProvider.UtcNow);
                await unitOfWork.SaveAsync();
                return Result.Success();
            }
            catch (Exception ex) when (DomainErrors.ToError(ex) is not null)
            {
                return Result.Failure(DomainErrors.ToError(ex)!);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                unitOfWork.GameRepository.Detach(game);
            }
        }
    }
}
