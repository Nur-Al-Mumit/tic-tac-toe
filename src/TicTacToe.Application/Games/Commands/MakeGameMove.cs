using Cortex.Mediator.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Commands;

public sealed record MakeGameMoveCommand(Guid GameId, int Position) : ICommand<Result<GameDto>>;

public sealed class MakeGameMoveCommandValidator : AbstractValidator<MakeGameMoveCommand>
{
    public MakeGameMoveCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.Position).InclusiveBetween(0, 8);
    }
}

/// <summary>Server-authoritative move (US-08): guards live in the aggregate; the race loser reloads once (ADR-0006).</summary>
public sealed class MakeGameMoveCommandHandler(
    IGameRepository gameRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    GameDtoLoader dtoLoader) : ICommandHandler<MakeGameMoveCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(MakeGameMoveCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } playerId)
        {
            return Result.Failure<GameDto>(Error.Unauthorized());
        }

        Game? game = null;
        for (var attempt = 0; ; attempt++)
        {
            game = await gameRepository.GetByIdAsync(command.GameId, cancellationToken);
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
                await unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (Exception ex) when (DomainErrors.ToError(ex) is not null)
            {
                return Result.Failure<GameDto>(DomainErrors.ToError(ex)!);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                gameRepository.Detach(game); // stale copy out; next iteration re-evaluates on fresh state
            }
        }

        return await dtoLoader.LoadAsync(game!, cancellationToken);
    }
}
