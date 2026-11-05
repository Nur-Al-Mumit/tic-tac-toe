using Cortex.Mediator.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Commands;

public sealed record CancelGameCommand(Guid GameId) : ICommand<Result>;

public sealed class CancelGameCommandValidator : AbstractValidator<CancelGameCommand>
{
    public CancelGameCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}

/// <summary>US-12: only the host, only while Waiting. If the game was joined in the meantime, the reload-once path returns Games.NotInProgress.</summary>
public sealed class CancelGameCommandHandler(
    IGameRepository gameRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CancelGameCommand, Result>
{
    public async Task<Result> Handle(CancelGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } playerId)
        {
            return Result.Failure(Error.Unauthorized());
        }

        for (var attempt = 0; ; attempt++)
        {
            var game = await gameRepository.GetByIdAsync(command.GameId, cancellationToken);
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
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (Exception ex) when (DomainErrors.ToError(ex) is not null)
            {
                return Result.Failure(DomainErrors.ToError(ex)!);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                gameRepository.Detach(game);
            }
        }
    }
}
