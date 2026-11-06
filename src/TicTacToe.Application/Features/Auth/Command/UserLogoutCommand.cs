using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed record UserLogoutCommand(string RefreshToken) : ICommand<Result>;

public sealed class UserLogoutCommandValidator : AbstractValidator<UserLogoutCommand>
{
    public UserLogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
