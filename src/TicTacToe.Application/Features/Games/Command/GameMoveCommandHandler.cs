using Cortex.Mediator.Commands;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>Server-authoritative move (US-08): guards live in the aggregate; the race loser reloads once.</summary>
public sealed class GameMoveCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    GameDtoLoader dtoLoader) : ICommandHandler<GameMoveCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GameMoveCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } playerId)
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

            if (!game.IsParticipant(playerId))
            {
                return Result.Failure<GameDto>(new Error("Games.Forbidden", "You are not a player in this game."));
            }

            try
            {
                game.MakeMove(playerId, command.Position, dateTimeProvider.UtcNow);
                await unitOfWork.SaveAsync();
                break;
            }
            catch (Exception ex) when (DomainErrors.ToError(ex) is not null)
            {
                return Result.Failure<GameDto>(DomainErrors.ToError(ex)!);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                unitOfWork.GameRepository.Detach(game); // stale copy out; next iteration re-evaluates on fresh state
            }
        }

        return await dtoLoader.LoadAsync(game!, cancellationToken);
    }
}
