using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
