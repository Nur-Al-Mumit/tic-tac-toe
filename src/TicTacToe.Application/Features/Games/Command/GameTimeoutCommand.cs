using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>Invoked by the timeout sweeper on behalf of the player whose clock expired.</summary>
public sealed record GameTimeoutCommand(Guid GameId, Guid PlayerId) : ICommand<Result>;

public sealed class GameTimeoutCommandValidator : AbstractValidator<GameTimeoutCommand>
{
    public GameTimeoutCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
    }
}
