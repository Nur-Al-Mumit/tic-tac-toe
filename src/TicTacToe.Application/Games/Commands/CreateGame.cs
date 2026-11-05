using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.Commands;

public sealed record CreateGameCommand : ICommand<Result<GameDto>>;

public sealed class CreateGameCommandValidator : AbstractValidator<CreateGameCommand>;

public sealed class CreateGameCommandHandler(
    IGameRepository gameRepository,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    GameDtoLoader dtoLoader) : ICommandHandler<CreateGameCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } hostId)
        {
            return Result.Failure<GameDto>(Error.Unauthorized());
        }

        if (await gameRepository.GetActiveGameForPlayerAsync(hostId, cancellationToken) is not null)
        {
            return Result.Failure<GameDto>(new Error("Games.Busy", "You already have an active game."));
        }

        var game = Game.Create(hostId, dateTimeProvider.UtcNow);
        await gameRepository.AddAsync(game, cancellationToken);

        return await dtoLoader.LoadAsync(game, cancellationToken);
    }
}
