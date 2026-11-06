using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Command;

public sealed record GameJoinCommand(Guid GameId) : ICommand<Result<GameDto>>;

public sealed class GameJoinCommandValidator : AbstractValidator<GameJoinCommand>
{
    public GameJoinCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
