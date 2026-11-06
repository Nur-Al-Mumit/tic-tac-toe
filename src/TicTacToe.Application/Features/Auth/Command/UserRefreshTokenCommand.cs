using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed record UserRefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResponse>>;

public sealed class UserRefreshTokenCommandValidator : AbstractValidator<UserRefreshTokenCommand>
{
    public UserRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
