using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Application.Features.Games.Command;

public sealed class GameLeaveCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<GameLeaveCommand, Result>
{
    public async Task<Result> Handle(GameLeaveCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var game = await unitOfWork.GameRepository.GetGameByIdAsync(command.GameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure(new Error("Games.NotFound", "Game not found."));
            }

            if (!game.IsParticipant(command.PlayerId))
            {
                return Result.Failure(new Error("Games.Forbidden", "You are not a player in this game."));
            }

            try
            {
                game.Forfeit(command.PlayerId, dateTimeProvider.UtcNow);
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
