using Cortex.Mediator.Commands;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Auth.Commands;

public sealed class RegisterUserCommandHandler(IIdentityService identityService, ITokenService tokenService)
    : ICommandHandler<RegisterUserCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var user = await identityService.RegisterAsync(command.Username, command.Email, command.Password, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        return await tokenService.IssueTokensAsync(user.Value.Id, user.Value.Username, user.Value.Email, null, cancellationToken);
    }
}

public sealed class LoginCommandHandler(IIdentityService identityService, ITokenService tokenService)
    : ICommandHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identityService.ValidateCredentialsAsync(command.Identifier, command.Password, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<AuthResponse>(user.Error!);
        }

        return await tokenService.IssueTokensAsync(user.Value.Id, user.Value.Username, user.Value.Email, null, cancellationToken);
    }
}

public sealed class RefreshTokenCommandHandler(ITokenService tokenService)
    : ICommandHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
        => await tokenService.RefreshAsync(command.RefreshToken, null, cancellationToken);
}

public sealed class LogoutCommandHandler(ITokenService tokenService, ICurrentUser currentUser)
    : ICommandHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is { } ownerId)
        {
            await tokenService.RevokeAsync(command.RefreshToken, ownerId, cancellationToken);
        }

        return Result.Success();
    }
}
