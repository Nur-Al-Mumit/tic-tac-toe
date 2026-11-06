using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Command;

public sealed class GameWaitingExpiredCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<GameWaitingExpiredCommand, Result>
{
    public async Task<Result> Handle(GameWaitingExpiredCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var game = await unitOfWork.GameRepository.GetGameByIdAsync(command.GameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure(new Error("Games.NotFound", "Game not found."));
            }

            // Joined in the meantime, or already terminal — nothing to expire.
            if (game.Status != GameStatus.Waiting
                || game.MoveDeadlineUtc is not { } deadline
                || deadline > dateTimeProvider.UtcNow)
            {
                return Result.Success();
            }

            try
            {
                game.ExpireWaiting(dateTimeProvider.UtcNow);
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
