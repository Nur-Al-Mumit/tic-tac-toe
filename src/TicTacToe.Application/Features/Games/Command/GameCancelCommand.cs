using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Features.Games.Command;

public sealed record GameCancelCommand(Guid GameId) : ICommand<Result>;

public sealed class GameCancelCommandValidator : AbstractValidator<GameCancelCommand>
{
    public GameCancelCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
