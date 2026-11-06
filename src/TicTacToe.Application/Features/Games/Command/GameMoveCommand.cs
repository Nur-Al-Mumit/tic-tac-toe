using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Command;

public sealed record GameMoveCommand(Guid GameId, int Position) : ICommand<Result<GameDto>>;

public sealed class GameMoveCommandValidator : AbstractValidator<GameMoveCommand>
{
    public GameMoveCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.Position).InclusiveBetween(0, 8);
    }
}
