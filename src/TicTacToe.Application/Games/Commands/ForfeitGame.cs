using Cortex.Mediator.Commands;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Commands;

/// <summary>playerId is explicit (not from ICurrentUser) so the V1.1 DisconnectGraceService can forfeit on behalf of a disconnected player.</summary>
public sealed record ForfeitGameCommand(Guid GameId, Guid PlayerId) : ICommand<Result>;

public sealed class ForfeitGameCommandValidator : AbstractValidator<ForfeitGameCommand>
{
    public ForfeitGameCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
    }
}

public sealed class ForfeitGameCommandHandler(
    IGameRepository gameRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ForfeitGameCommand, Result>
{
    public async Task<Result> Handle(ForfeitGameCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var game = await gameRepository.GetByIdAsync(command.GameId, cancellationToken);
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
