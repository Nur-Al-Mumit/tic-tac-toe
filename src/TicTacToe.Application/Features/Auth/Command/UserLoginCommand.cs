using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed record UserLoginCommand(string Identifier, string Password) : ICommand<Result<AuthResponse>>;

public sealed class UserLoginCommandValidator : AbstractValidator<UserLoginCommand>
{
    public UserLoginCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
