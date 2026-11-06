using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>Sent by the sweeper when a Waiting game's clock runs out — the game cancels, the host is freed.</summary>
public sealed record GameWaitingExpiredCommand(Guid GameId) : ICommand<Result>;

public sealed class GameWaitingExpiredCommandValidator : AbstractValidator<GameWaitingExpiredCommand>
{
    public GameWaitingExpiredCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
