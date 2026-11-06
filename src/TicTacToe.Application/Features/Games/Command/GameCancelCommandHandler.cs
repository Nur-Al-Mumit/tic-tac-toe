using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>US-12: only the host, only while Waiting; a mid-flight join makes the reload-once path return NotInProgress.</summary>
public sealed class GameCancelCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<GameCancelCommand, Result>
{
    public async Task<Result> Handle(GameCancelCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } playerId)
        {
            return Result.Failure(Error.Unauthorized());
        }

        for (var attempt = 0; ; attempt++)
        {
            var game = await unitOfWork.GameRepository.GetGameByIdAsync(command.GameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure(new Error("Games.NotFound", "Game not found."));
            }

            if (game.PlayerXId != playerId)
            {
                return Result.Failure(new Error("Games.NotHost", "Only the host can cancel a waiting game."));
            }

            try
            {
                game.Cancel(playerId, dateTimeProvider.UtcNow);
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
