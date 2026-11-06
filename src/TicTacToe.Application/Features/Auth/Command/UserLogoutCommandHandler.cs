using Cortex.Mediator.Commands;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed class UserLogoutCommandHandler(ITokenService tokenService, ICurrentUser currentUser)
    : ICommandHandler<UserLogoutCommand, Result>
{
    public async Task<Result> Handle(UserLogoutCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is { } ownerId)
        {
            await tokenService.RevokeAsync(command.RefreshToken, ownerId, cancellationToken);
        }

        return Result.Success();
    }
}
