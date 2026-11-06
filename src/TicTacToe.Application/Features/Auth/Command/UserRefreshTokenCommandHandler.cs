using Cortex.Mediator.Commands;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed class UserRefreshTokenCommandHandler(ITokenService tokenService)
    : ICommandHandler<UserRefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(UserRefreshTokenCommand command, CancellationToken cancellationToken)
        => await tokenService.RefreshAsync(command.RefreshToken, null, cancellationToken);
}
