using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Auth.Commands;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
