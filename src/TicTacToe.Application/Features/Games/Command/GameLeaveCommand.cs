using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Features.Games.Command;

/// <summary>playerId is explicit (not from ICurrentUser) so the V1.1 DisconnectGraceService can forfeit on behalf of a disconnected player.</summary>
public sealed record GameLeaveCommand(Guid GameId, Guid PlayerId) : ICommand<Result>;

public sealed class GameLeaveCommandValidator : AbstractValidator<GameLeaveCommand>
{
    public GameLeaveCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
    }
}
