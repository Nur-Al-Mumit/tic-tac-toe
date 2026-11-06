using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed record UserRegisterCommand(string Username, string Email, string Password) : ICommand<Result<AuthResponse>>;

public sealed class UserRegisterCommandValidator : AbstractValidator<UserRegisterCommand>
{
    public UserRegisterCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .Matches("^[A-Za-z0-9_]{3,20}$")
            .WithMessage("Username must be 3-20 characters: letters, digits and underscore only.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
