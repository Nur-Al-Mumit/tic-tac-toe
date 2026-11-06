using Cortex.Mediator.Commands;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Features.Auth.Command;

public sealed class UserLoginCommandHandler(IIdentityService identityService, ITokenService tokenService)
    : ICommandHandler<UserLoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(UserLoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identityService.ValidateCredentialsAsync(command.Identifier, command.Password, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        return await tokenService.IssueTokensAsync(user.Value.Id, user.Value.Username, user.Value.Email, null, cancellationToken);
    }
}
