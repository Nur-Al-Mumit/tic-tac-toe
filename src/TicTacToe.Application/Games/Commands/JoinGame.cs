using Cortex.Mediator.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Commands;

public sealed record JoinGameCommand(Guid GameId) : ICommand<Result<GameDto>>;

public sealed class JoinGameCommandValidator : AbstractValidator<JoinGameCommand>
{
    public JoinGameCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}

/// <summary>
/// Race-safe join (US-07): two simultaneous joiners load the same Waiting game; the loser of the
/// xmin save detaches, reloads once, re-evaluates the domain rules on fresh state and gets Games.Full.
/// </summary>
public sealed class JoinGameCommandHandler(
    IGameRepository gameRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameDtoLoader dtoLoader,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<JoinGameCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(JoinGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } joinerId)
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

            if (game.Status != GameStatus.Waiting)
            {
                return Result.Failure<GameDto>(new Error("Games.Full", "Game is already full."));
            }

            if (game.PlayerXId == joinerId)
            {
                return Result.Failure<GameDto>(new Error("Games.CannotJoinOwnGame", "You cannot join your own game."));
            }

            var activeGame = await gameRepository.GetActiveGameForPlayerAsync(joinerId, cancellationToken);
            if (activeGame is not null && activeGame.Id != command.GameId)
            {
                return Result.Failure<GameDto>(new Error("Games.Busy", "You already have an active game."));
            }

            try
            {
                game.Join(joinerId, dateTimeProvider.UtcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                gameRepository.Detach(game); // drop the stale tracked copy so the next load is fresh
            }
        }

        return await dtoLoader.LoadAsync(game!, cancellationToken);
    }

}
