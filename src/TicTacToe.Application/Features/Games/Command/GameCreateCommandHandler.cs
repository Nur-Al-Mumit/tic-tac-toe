using Cortex.Mediator.Commands;
using Microsoft.Extensions.Options;
using TicTacToe.Application.Common;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Command;

public sealed class GameCreateCommandHandler(
    IApplicationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    GameDtoLoader dtoLoader,
    IOptions<GameRulesOptions> gameRules) : ICommandHandler<GameCreateCommand, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GameCreateCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } hostId)
        {
            return Result.Failure<GameDto>(Error.Unauthorized());
        }

        if (await unitOfWork.GameRepository.GetActiveGameForPlayerAsync(hostId, cancellationToken) is not null)
        {
            return Result.Failure<GameDto>(new Error("Games.Busy", "You already have an active game."));
        }

        var game = Game.Create(hostId, dateTimeProvider.UtcNow, gameRules.Value.WaitingTimeout);
        await unitOfWork.GameRepository.AddAsync(game, cancellationToken);
        await unitOfWork.SaveAsync();

        return await dtoLoader.LoadAsync(game, cancellationToken);
    }
}
