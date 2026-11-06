using Cortex.Mediator.Commands;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed class UserRegisterCommandHandler(IIdentityService identityService, ITokenService tokenService)
    : ICommandHandler<UserRegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(UserRegisterCommand command, CancellationToken cancellationToken)
    {
        var user = await identityService.RegisterAsync(command.Username, command.Email, command.Password, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        return await tokenService.IssueTokensAsync(user.Value.Id, user.Value.Username, user.Value.Email, null, cancellationToken);
    }
}
