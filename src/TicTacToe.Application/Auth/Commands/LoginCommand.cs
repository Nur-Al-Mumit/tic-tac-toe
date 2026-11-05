using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Auth.Commands;

public sealed record LoginCommand(string Identifier, string Password) : ICommand<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
